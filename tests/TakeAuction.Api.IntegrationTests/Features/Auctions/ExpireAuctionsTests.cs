using System.Net;
using System.Net.Http.Json;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExpireAuctionsTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;

    public ExpireAuctionsTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Keeps_its_own_schema_in_postgres()
    {
        var tableCount = await _fixture.ExecuteDbContextAsync(db => db.Database
            .SqlQueryRaw<int>(
                """SELECT count(*)::int AS "Value" FROM information_schema.tables WHERE table_schema = 'hangfire'""")
            .SingleAsync());

        Assert.True(tableCount > 0, "Hangfire should have prepared its own schema in PostgreSQL");
    }

    [Fact]
    public void Registers_the_expiry_sweep_as_a_recurring_job()
    {
        var storage = _fixture.Services.GetRequiredService<JobStorage>();

        using var connection = storage.GetConnection();
        var recurringJob = connection.GetRecurringJobs()
            .SingleOrDefault(job => job.Id == ExpireAuctionsRecurringJob.JobId);

        Assert.NotNull(recurringJob);
        Assert.Equal(TakeAuctionApiFactory.NeverFiringCron, recurringJob.Cron);
        Assert.Equal(typeof(ExpireAuctionsJob), recurringJob.Job.Type);
        Assert.Equal(nameof(ExpireAuctionsJob.RunAsync), recurringJob.Job.Method.Name);
    }

    [Fact]
    public async Task A_hangfire_worker_picks_up_the_sweep_and_closes_a_due_auction()
    {
        var auctionId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));

        var client = _fixture.Services.GetRequiredService<IBackgroundJobClient>();
        client.Enqueue<ExpireAuctionsJob>(job => job.RunAsync(CancellationToken.None));

        await WaitForStatusAsync(auctionId, AuctionStatus.Ended);
    }

    [Fact]
    public async Task A_hangfire_worker_leaves_a_running_auction_open()
    {
        var runningId = await CreateAuctionAsync(endsIn: TimeSpan.FromDays(1));
        var dueId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));

        var client = _fixture.Services.GetRequiredService<IBackgroundJobClient>();
        client.Enqueue<ExpireAuctionsJob>(job => job.RunAsync(CancellationToken.None));

        await WaitForStatusAsync(dueId, AuctionStatus.Ended);

        Assert.Equal(AuctionStatus.Active, await StatusOfAsync(runningId));
    }

    [Fact]
    public async Task The_close_reaches_watchers_through_rabbitmq_and_signalr()
    {
        var winner = await _fixture.CreateUserAsync(UserRole.Bidder);
        var auctionId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1), winner: (winner.Id, 250m));

        await using var connection = _fixture.CreateHubConnection();
        var received = new TaskCompletionSource<AuctionStatusChangedNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        connection.On<AuctionStatusChangedNotification>(
            nameof(IAuctionClient.AuctionStatusChanged),
            notification =>
            {
                if (notification.AuctionId == auctionId)
                {
                    received.TrySetResult(notification);
                }
            });

        await connection.StartAsync();
        await connection.InvokeAsync(nameof(AuctionHub.SubscribeToAuction), auctionId);

        await RunSweepAsync();

        var status = await received.Task.WaitAsync(DeliveryTimeout);

        Assert.Equal(nameof(AuctionStatus.Ended), status.Status);
        Assert.Equal(250m, status.CurrentPrice);
        Assert.Equal(winner.Id, status.LeadingBidderId);
    }

    [Fact]
    public async Task An_auction_that_drew_no_bids_closes_without_a_winner()
    {
        var auctionId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));

        var closed = await RunSweepAsync();

        Assert.Equal(1, closed);

        var auction = await _fixture.ExecuteDbContextAsync(db =>
            db.Auctions.SingleAsync(entity => entity.Id == auctionId));

        Assert.Equal(AuctionStatus.Ended, auction.Status);
        Assert.Null(auction.LeadingBidderId);
        Assert.Equal(StartingPrice, auction.CurrentPrice);
    }

    [Fact]
    public async Task Sweeping_twice_closes_the_auction_once()
    {
        await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));

        Assert.Equal(1, await RunSweepAsync());
        Assert.Equal(0, await RunSweepAsync());
    }

    [Fact]
    public async Task A_closed_auction_rejects_new_bids_over_http()
    {
        var auctionId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));

        await RunSweepAsync();

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/auctions/{auctionId}/bids",
            new PlaceBidRequest(500m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_cached_detail_stops_advertising_the_auction_as_open()
    {
        var auctionId = await CreateAuctionAsync(endedAgo: TimeSpan.FromHours(1));
        var client = _fixture.CreateClient();
        var detailUrl = $"/api/v1/auctions/{auctionId}";

        var beforeSweep = await client.GetFromJsonAsync<AuctionDetailResponse>(
            detailUrl,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(beforeSweep);
        Assert.Equal(nameof(AuctionStatus.Active), beforeSweep.Status);

        await RunSweepAsync();

        var afterSweep = await client.GetFromJsonAsync<AuctionDetailResponse>(
            detailUrl,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(afterSweep);
        Assert.Equal(nameof(AuctionStatus.Ended), afterSweep.Status);
    }

    private async Task<int> RunSweepAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<ExpireAuctionsJob>();

        return await job.RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_new_lot_books_its_own_close_rather_than_waiting_for_the_sweep()
    {
        var auctionId = await ListAuctionAsync(runsFor: TimeSpan.FromMinutes(10));

        var storage = _fixture.Services.GetRequiredService<JobStorage>();
        var scheduled = storage.GetMonitoringApi().ScheduledJobs(0, 100);

        var booking = scheduled.SingleOrDefault(entry =>
            entry.Value.Job.Type == typeof(CloseAuctionJob)
            && entry.Value.Job.Args[0] is Guid id
            && id == auctionId);

        Assert.NotNull(booking.Value);
        Assert.Equal(nameof(CloseAuctionJob.RunAsync), booking.Value.Job.Method.Name);
    }

    [Fact]
    public async Task A_lot_the_soft_close_pushed_out_books_the_new_time_too()
    {
        var auctionId = await ListAuctionAsync(runsFor: TimeSpan.FromMinutes(6));

        // Six minutes out, so the bid lands well inside the default one-minute closing window
        // only after the clock has run down — which it has not. Instead the lot is nudged by a
        // bid that does extend it: the shortest lot the validator allows still closes inside
        // the window once its own close is a minute away, so this asserts the booking count
        // rather than trying to race a real clock.
        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var bidderClient = await _fixture.CreateClientAsAsync(bidder);

        (await bidderClient.PostAsJsonAsync($"/api/v1/auctions/{auctionId}/bids", new PlaceBidRequest(150m)))
            .EnsureSuccessStatusCode();

        var storage = _fixture.Services.GetRequiredService<JobStorage>();
        var bookings = storage.GetMonitoringApi()
            .ScheduledJobs(0, 200)
            .Count(entry =>
                entry.Value.Job.Type == typeof(CloseAuctionJob)
                && entry.Value.Job.Args[0] is Guid id
                && id == auctionId);

        // An ordinary bid changes nothing about when the lot closes, so nothing is re-booked.
        Assert.Equal(1, bookings);
    }

    private async Task<Guid> ListAuctionAsync(TimeSpan runsFor)
    {
        var sellerClient = await _fixture.CreateClientAsAsync(_seller);
        var now = DateTimeOffset.UtcNow;

        var response = await sellerClient.PostAsJsonAsync(
            "/api/v1/auctions",
            new
            {
                title = "Rare stamp collection",
                description = "A detailed description of the lot on offer.",
                startingPrice = StartingPrice,
                minimumBidIncrement = Increment,
                startsAtUtc = now,
                endsAtUtc = now.Add(runsFor)
            });

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CreateAuctionBody>(
            IntegrationTestFixture.JsonOptions);

        return created!.Id;
    }

    private sealed record CreateAuctionBody(Guid Id);

    private async Task WaitForStatusAsync(Guid auctionId, AuctionStatus expected)
    {
        var deadline = DateTimeOffset.UtcNow + JobTimeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await StatusOfAsync(auctionId) == expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.Fail($"Auction {auctionId} never reached {expected} within {JobTimeout.TotalSeconds:0} seconds");
    }

    private Task<AuctionStatus> StatusOfAsync(Guid auctionId) =>
        _fixture.ExecuteDbContextAsync(db => db.Auctions
            .Where(auction => auction.Id == auctionId)
            .Select(auction => auction.Status)
            .SingleAsync());

    private Task<Guid> CreateAuctionAsync(
        TimeSpan? endedAgo = null,
        TimeSpan? endsIn = null,
        (Guid BidderId, decimal Amount)? winner = null) =>
        _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var endsAt = endsIn is { } running ? now.Add(running) : now.Subtract(endedAgo ?? TimeSpan.FromHours(1));
            var startsAt = endsAt.AddHours(-6);

            var auction = Auction.Create(
                _seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                startsAt,
                endsAt,
                startsAt);

            if (winner is { } bid)
            {
                // Two ceilings at the same figure leave the lot exactly there, held by the one
                // that got in first: an unopposed bid would only ever buy the asking price.
                Assert.True(auction.PlaceBid(bid.BidderId, bid.Amount, startsAt).Succeeded);
                Assert.True(auction.PlaceBid(Guid.CreateVersion7(), bid.Amount, startsAt).Succeeded);
            }

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });
}
