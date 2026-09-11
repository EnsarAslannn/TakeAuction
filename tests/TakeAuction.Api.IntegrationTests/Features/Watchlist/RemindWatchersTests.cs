using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Domain.Watchlist;
using TakeAuction.Api.Features.Watchlist.RemindWatchers;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Watchlist;

[Collection(IntegrationTestCollection.Name)]
public sealed class RemindWatchersTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;

    public RemindWatchersTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Reminds_everyone_watching_a_lot_that_closes_within_the_lead()
    {
        var auction = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(3));
        var watchers = await WatchAsync(auction, count: 3);

        var reminded = await RunAsync();

        Assert.Equal(3, reminded);

        var watches = await WatchesAsync();
        Assert.All(watches, watch => Assert.NotNull(watch.ClosingSoonNotifiedAtUtc));

        var events = await QueuedRemindersAsync();
        Assert.Equal(watchers.Order(), events.Select(e => e.UserId).Order());
        Assert.All(events, e =>
        {
            Assert.Equal(auction.Id, e.AuctionId);
            Assert.Equal(auction.Title, e.AuctionTitle);
            Assert.Equal(auction.EndsAtUtc, e.EndsAtUtc, TimeSpan.FromMilliseconds(1));
        });
    }

    [Fact]
    public async Task Leaves_a_lot_that_closes_after_the_lead_alone()
    {
        var auction = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(20));
        await WatchAsync(auction, count: 1);

        Assert.Equal(0, await RunAsync());
        Assert.Null((await WatchesAsync()).Single().ClosingSoonNotifiedAtUtc);
        Assert.Empty(await QueuedRemindersAsync());
    }

    [Fact]
    public async Task Never_reminds_the_same_watcher_twice()
    {
        var auction = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(3));
        await WatchAsync(auction, count: 2);

        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, (await QueuedRemindersAsync()).Count);
    }

    [Fact]
    public async Task Skips_lots_that_have_already_closed_or_were_withdrawn()
    {
        var ended = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(-1));
        var withdrawn = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(3));

        await WatchAsync(ended, count: 1);
        await WatchAsync(withdrawn, count: 1);

        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            var lot = await dbContext.Auctions.SingleAsync(a => a.Id == withdrawn.Id);
            lot.Cancel(_seller.Id, DateTimeOffset.UtcNow);
            return await dbContext.SaveChangesAsync();
        });

        Assert.Equal(0, await RunAsync());
    }

    [Fact]
    public async Task Two_sweeps_running_at_once_remind_each_watcher_exactly_once()
    {
        var auctions = new List<Auction>();
        for (var i = 0; i < 4; i++)
        {
            auctions.Add(await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(1 + i)));
        }

        foreach (var auction in auctions)
        {
            await WatchAsync(auction, count: 10);
        }

        var runs = await Task.WhenAll(RunAsync(), RunAsync(), RunAsync());

        Assert.Equal(40, runs.Sum());

        var events = await QueuedRemindersAsync();
        Assert.Equal(40, events.Count);
        Assert.Equal(40, events.Select(e => (e.UserId, e.AuctionId)).Distinct().Count());
    }

    [Fact]
    public async Task The_reminder_reaches_the_watchers_inbox_through_the_broker()
    {
        var auction = await SeedAuctionAsync(closesIn: TimeSpan.FromMinutes(3));
        var watcher = (await WatchAsync(auction, count: 1)).Single();

        await RunAsync();
        await _fixture.WaitForOutboxDrainAsync();

        var notification = await WaitForNotificationAsync(watcher);

        Assert.Equal(NotificationKind.AuctionClosingSoon, notification.Kind);
        Assert.Equal(auction.Id, notification.AuctionId);
        Assert.Null(notification.ReadAtUtc);
    }

    private async Task<int> RunAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<RemindWatchersJob>().RunAsync(CancellationToken.None);
    }

    private async Task<Auction> SeedAuctionAsync(TimeSpan closesIn)
    {
        var now = DateTimeOffset.UtcNow;
        var endsAt = now.Add(closesIn);

        var auction = Auction.Create(
            _seller.Id,
            $"Watched lot {Guid.CreateVersion7():N}",
            "A lot under test.",
            100m,
            5m,
            endsAt.AddHours(-2),
            endsAt,
            endsAt.AddHours(-2));

        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Auctions.Add(auction);
            return await dbContext.SaveChangesAsync();
        });

        return auction;
    }

    private async Task<IReadOnlyList<Guid>> WatchAsync(Auction auction, int count)
    {
        var watchers = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            watchers.Add((await _fixture.CreateUserAsync(UserRole.Bidder)).Id);
        }

        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.AuctionWatches.AddRange(
                watchers.Select(id => AuctionWatch.Start(id, auction.Id, DateTimeOffset.UtcNow)));
            return await dbContext.SaveChangesAsync();
        });

        return watchers;
    }

    private Task<List<AuctionWatch>> WatchesAsync() =>
        _fixture.ExecuteDbContextAsync(dbContext => dbContext.AuctionWatches.AsNoTracking().ToListAsync());

    private async Task<List<WatchedAuctionClosingSoonIntegrationEvent>> QueuedRemindersAsync()
    {
        var payloads = await _fixture.ExecuteDbContextAsync(dbContext => dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Type == nameof(WatchedAuctionClosingSoonIntegrationEvent))
            .Select(message => message.Payload)
            .ToListAsync());

        return payloads
            .Select(payload => JsonSerializer.Deserialize<WatchedAuctionClosingSoonIntegrationEvent>(
                payload, Outbox.SerializerOptions)!)
            .ToList();
    }

    private async Task<Notification> WaitForNotificationAsync(Guid userId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var notification = await _fixture.ExecuteDbContextAsync(dbContext => dbContext.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == userId));

            if (notification is not null)
            {
                return notification;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No notification reached user {userId}.");
    }
}
