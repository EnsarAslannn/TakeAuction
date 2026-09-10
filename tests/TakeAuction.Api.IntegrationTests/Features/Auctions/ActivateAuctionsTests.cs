using System.Net.Http.Json;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.OpenAuctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class ActivateAuctionsTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private static readonly TimeSpan JobTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DeliveryTimeout = TimeSpan.FromSeconds(30);

    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;

    public ActivateAuctionsTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Registers_the_opening_sweep_as_a_recurring_job()
    {
        var storage = _fixture.Services.GetRequiredService<JobStorage>();

        using var connection = storage.GetConnection();
        var recurringJob = connection.GetRecurringJobs()
            .SingleOrDefault(job => job.Id == ActivateAuctionsRecurringJob.JobId);

        Assert.NotNull(recurringJob);
        Assert.Equal(TakeAuctionApiFactory.NeverFiringCron, recurringJob.Cron);
        Assert.Equal(typeof(ActivateAuctionsJob), recurringJob.Job.Type);
        Assert.Equal(nameof(ActivateAuctionsJob.RunAsync), recurringJob.Job.Method.Name);
    }

    [Fact]
    public async Task A_hangfire_worker_picks_up_the_sweep_and_opens_a_due_auction()
    {
        var auctionId = await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));

        var client = _fixture.Services.GetRequiredService<IBackgroundJobClient>();
        client.Enqueue<ActivateAuctionsJob>(job => job.RunAsync(CancellationToken.None));

        await WaitForStatusAsync(auctionId, AuctionStatus.Active);
    }

    [Fact]
    public async Task A_hangfire_worker_leaves_a_lot_that_has_not_started_scheduled()
    {
        var waitingId = await CreateScheduledAuctionAsync(startsIn: TimeSpan.FromDays(1));
        var dueId = await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));

        var client = _fixture.Services.GetRequiredService<IBackgroundJobClient>();
        client.Enqueue<ActivateAuctionsJob>(job => job.RunAsync(CancellationToken.None));

        await WaitForStatusAsync(dueId, AuctionStatus.Active);

        Assert.Equal(AuctionStatus.Scheduled, await StatusOfAsync(waitingId));
    }

    [Fact]
    public async Task The_opening_reaches_watchers_through_rabbitmq_and_signalr()
    {
        var auctionId = await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));

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

        Assert.Equal(nameof(AuctionStatus.Active), status.Status);
        Assert.Equal(StartingPrice, status.CurrentPrice);
        Assert.Null(status.LeadingBidderId);
    }

    [Fact]
    public async Task Sweeping_twice_opens_the_auction_once()
    {
        await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));

        Assert.Equal(1, await RunSweepAsync());
        Assert.Equal(0, await RunSweepAsync());
    }

    [Fact]
    public async Task An_opened_lot_takes_bids_over_http()
    {
        var auctionId = await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));

        await RunSweepAsync();

        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/auctions/{auctionId}/bids",
            new PlaceBidRequest(StartingPrice));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_cached_detail_stops_advertising_the_lot_as_scheduled()
    {
        var auctionId = await CreateScheduledAuctionAsync(startedAgo: TimeSpan.FromHours(1));
        var client = _fixture.CreateClient();
        var detailUrl = $"/api/v1/auctions/{auctionId}";

        var beforeSweep = await client.GetFromJsonAsync<AuctionDetailResponse>(
            detailUrl,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(beforeSweep);
        Assert.Equal(nameof(AuctionStatus.Scheduled), beforeSweep.Status);

        await RunSweepAsync();

        var afterSweep = await client.GetFromJsonAsync<AuctionDetailResponse>(
            detailUrl,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(afterSweep);
        Assert.Equal(nameof(AuctionStatus.Active), afterSweep.Status);
    }

    [Fact]
    public async Task A_lot_listed_for_a_later_start_books_its_own_opening()
    {
        var auctionId = await ListAuctionAsync(startsIn: TimeSpan.FromHours(3));

        var storage = _fixture.Services.GetRequiredService<JobStorage>();
        var scheduled = storage.GetMonitoringApi().ScheduledJobs(0, 200);

        var booking = scheduled.SingleOrDefault(entry =>
            entry.Value.Job.Type == typeof(OpenAuctionJob)
            && entry.Value.Job.Args[0] is Guid id
            && id == auctionId);

        Assert.NotNull(booking.Value);
        Assert.Equal(nameof(OpenAuctionJob.RunAsync), booking.Value.Job.Method.Name);
    }

    [Fact]
    public async Task A_lot_that_opens_immediately_books_no_opening()
    {
        var auctionId = await ListAuctionAsync(startsIn: TimeSpan.Zero);

        var storage = _fixture.Services.GetRequiredService<JobStorage>();
        var bookings = storage.GetMonitoringApi()
            .ScheduledJobs(0, 200)
            .Count(entry =>
                entry.Value.Job.Type == typeof(OpenAuctionJob)
                && entry.Value.Job.Args[0] is Guid id
                && id == auctionId);

        Assert.Equal(0, bookings);
    }

    private async Task<int> RunSweepAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetRequiredService<ActivateAuctionsJob>();

        return await job.RunAsync(CancellationToken.None);
    }

    private async Task<Guid> ListAuctionAsync(TimeSpan startsIn)
    {
        var sellerClient = await _fixture.CreateClientAsAsync(_seller);
        var now = DateTimeOffset.UtcNow;
        var startsAt = now.Add(startsIn);

        var response = await sellerClient.PostAsJsonAsync(
            "/api/v1/auctions",
            new
            {
                title = "Rare stamp collection",
                description = "A detailed description of the lot on offer.",
                startingPrice = StartingPrice,
                minimumBidIncrement = Increment,
                startsAtUtc = startsAt,
                endsAtUtc = startsAt.AddHours(6)
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

    private Task<Guid> CreateScheduledAuctionAsync(TimeSpan? startedAgo = null, TimeSpan? startsIn = null) =>
        _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;

            var startsAt = startsIn is { } waiting
                ? now.Add(waiting)
                : now.Subtract(startedAgo ?? TimeSpan.FromHours(1));

            var auction = Auction.Create(
                _seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                startsAt,
                startsAt.AddDays(1),
                startsAt.AddHours(-1));

            Assert.Equal(AuctionStatus.Scheduled, auction.Status);

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });
}
