using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Notifications;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Notifications;

[Collection(IntegrationTestCollection.Name)]
public sealed class NotificationDeliveryTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;
    private User _bidder = null!;

    public NotificationDeliveryTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
        _bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Concurrent_deliveries_of_the_same_notification_leave_one_row()
    {
        var auction = await SeedAuctionAsync(TimeSpan.FromHours(1));

        var deliveries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => DeliverAsync(auction)));

        Assert.Equal(1, deliveries.Sum());
        Assert.Equal(1, await CountAsync(_bidder.Id));
    }

    [Fact]
    public async Task The_winner_and_the_seller_hear_about_a_close()
    {
        var auction = await SeedAuctionAsync(TimeSpan.FromSeconds(2));

        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            var lot = await dbContext.Auctions.SingleAsync(a => a.Id == auction.Id);
            lot.PlaceBid(_bidder.Id, 150m, DateTimeOffset.UtcNow);
            return await dbContext.SaveChangesAsync();
        });

        await Task.Delay(TimeSpan.FromSeconds(2.5));

        await using (var scope = _fixture.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider
                .GetRequiredService<AuctionCloser>()
                .TryCloseAsync(auction.Id, CancellationToken.None));
        }

        await _fixture.WaitForOutboxDrainAsync();

        var won = await WaitForAsync(_bidder.Id);
        var sold = await WaitForAsync(_seller.Id);

        Assert.Equal(NotificationKind.AuctionWon, won.Kind);
        Assert.Equal(100m, won.Amount);
        Assert.Equal(NotificationKind.AuctionSold, sold.Kind);
        Assert.Equal(auction.Title, sold.AuctionTitle);
    }

    private async Task<int> DeliverAsync(Auction auction)
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var inbox = scope.ServiceProvider.GetRequiredService<NotificationInbox>();

        return await inbox.DeliverAsync(
            [
                Notification.Create(
                    _bidder.Id,
                    NotificationKind.AuctionClosingSoon,
                    auction.Id,
                    auction.Title,
                    null,
                    auction.EndsAtUtc,
                    DateTimeOffset.UtcNow)
            ],
            CancellationToken.None);
    }

    private async Task<Auction> SeedAuctionAsync(TimeSpan closesIn)
    {
        var now = DateTimeOffset.UtcNow;

        var auction = Auction.Create(
            _seller.Id,
            $"Delivered lot {Guid.CreateVersion7():N}",
            "A lot under test.",
            100m,
            5m,
            now.AddMinutes(-5),
            now.Add(closesIn),
            now.AddMinutes(-5),
            antiSnipeWindowSeconds: 0,
            antiSnipeExtensionSeconds: 0);

        await _fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Auctions.Add(auction);
            return await dbContext.SaveChangesAsync();
        });

        return auction;
    }

    private Task<int> CountAsync(Guid userId) =>
        _fixture.ExecuteDbContextAsync(dbContext => dbContext.Notifications.CountAsync(n => n.UserId == userId));

    private async Task<Notification> WaitForAsync(Guid userId)
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
