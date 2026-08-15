using MediatR;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.PlaceBid;

public sealed class PlaceBidTelemetryTests : IDisposable
{
    private readonly string _databaseName = $"placebid-telemetry-{Guid.CreateVersion7()}";
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly TakeAuctionTelemetry _telemetry = TestHarness.CreateTelemetry();
    private readonly MetricCollector _metrics;
    private readonly List<AppDbContext> _contexts = [];

    private Guid _auctionId;
    private Guid _sellerId;
    private Guid _bidderId;

    public PlaceBidTelemetryTests()
    {
        _metrics = new MetricCollector(_telemetry.Meter);

        Seed();
    }

    public void Dispose()
    {
        _metrics.Dispose();

        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    [Fact]
    public async Task An_accepted_bid_is_counted_as_one_that_settled_on_the_first_pass()
    {
        await CreateHandler().Handle(Command(150m), CancellationToken.None);

        var bid = Assert.Single(_metrics.For("takeauction.bids"));

        Assert.Equal(1, bid.Value);
        Assert.True(bid.Tagged("outcome", "accepted"));

        Assert.Equal(1, _metrics.Total("takeauction.bids.attempts"));
        Assert.Equal(0, _metrics.Total("takeauction.bids.concurrency_conflicts"));
    }

    [Fact]
    public async Task A_bid_that_lost_the_race_is_counted_as_a_conflict_and_a_second_pass()
    {
        var handler = CreateHandler(new ConcurrencyConflictInterceptor(conflictCount: 1));

        await handler.Handle(Command(150m), CancellationToken.None);

        Assert.Equal(1, _metrics.Total("takeauction.bids.concurrency_conflicts"));
        Assert.Equal(2, _metrics.Total("takeauction.bids.attempts"));

        var bid = Assert.Single(_metrics.For("takeauction.bids"));
        Assert.True(bid.Tagged("outcome", "accepted"));
    }

    [Fact]
    public async Task A_bid_that_ran_out_of_retries_is_counted_under_what_stopped_it()
    {
        var handler = CreateHandler(new ConcurrencyConflictInterceptor(conflictCount: int.MaxValue));

        await handler.Handle(Command(150m), CancellationToken.None);

        Assert.Equal(PlaceBidHandler.MaxAttempts, _metrics.Total("takeauction.bids.concurrency_conflicts"));

        var bid = Assert.Single(_metrics.For("takeauction.bids"));
        Assert.True(bid.Tagged("outcome", nameof(BidRejection.ConcurrencyConflict)));
    }

    [Fact]
    public async Task A_refused_bid_is_counted_under_the_reason_it_was_refused()
    {
        await CreateHandler().Handle(Command(1m), CancellationToken.None);

        var bid = Assert.Single(_metrics.For("takeauction.bids"));

        Assert.True(bid.Tagged("outcome", nameof(BidRejection.BidTooLow)));
    }

    [Fact]
    public async Task A_replayed_bid_is_counted_apart_from_the_one_it_replays()
    {
        var handler = CreateHandler();
        var key = Guid.CreateVersion7().ToString();

        await handler.Handle(Command(150m, key), CancellationToken.None);
        await handler.Handle(Command(150m, key), CancellationToken.None);

        var outcomes = _metrics.For("takeauction.bids");

        Assert.Equal(2, outcomes.Count);
        Assert.Single(outcomes, measurement => measurement.Tagged("outcome", "accepted"));
        Assert.Single(outcomes, measurement => measurement.Tagged("outcome", "replayed"));
    }

    [Fact]
    public async Task An_answer_the_house_placed_is_counted_separately_from_the_bid_that_caused_it()
    {
        var handler = CreateHandler();

        await handler.Handle(Command(500m), CancellationToken.None);

        _bidderId = AddUser(UserRole.Bidder);
        await handler.Handle(Command(300m), CancellationToken.None);

        Assert.Equal(1, _metrics.Total("takeauction.bids.proxy_answers"));
        Assert.Equal(2, _metrics.Total("takeauction.bids"));
    }

    [Fact]
    public async Task A_bid_that_pushed_the_close_out_is_counted()
    {
        var handler = CreateHandler();
        _timeProvider.Advance(TimeSpan.FromDays(2).Subtract(TimeSpan.FromSeconds(10)));

        await handler.Handle(Command(150m), CancellationToken.None);

        Assert.Equal(1, _metrics.Total("takeauction.auctions.extensions"));
    }

    [Fact]
    public async Task Every_settled_bid_reports_how_long_it_took()
    {
        await CreateHandler().Handle(Command(150m), CancellationToken.None);

        var duration = Assert.Single(_metrics.For("takeauction.bids.duration"));

        Assert.True(duration.Value >= 0);
        Assert.True(duration.Tagged("outcome", "accepted"));
    }

    private PlaceBidCommand Command(decimal amount, string? idempotencyKey = null) =>
        new(_auctionId, _bidderId, amount, idempotencyKey);

    private PlaceBidHandler CreateHandler(params IInterceptor[] interceptors)
    {
        var dbContext = NewContext(interceptors);

        return new PlaceBidHandler(
            dbContext,
            _timeProvider,
            _publisher,
            TestHarness.CreateOutbox(dbContext),
            _telemetry,
            NullLogger<PlaceBidHandler>.Instance);
    }

    private AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var context = TestHarness.CreateDbContext(_databaseName, interceptors);
        _contexts.Add(context);

        return context;
    }

    private Guid AddUser(UserRole role)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var user = User.Create($"{Guid.CreateVersion7():N}@takeauction.test", "Rival", "hash", role);
        context.Users.Add(user);
        context.SaveChanges();

        return user.Id;
    }

    private void Seed()
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var seller = User.Create("seller@takeauction.test", "Demo Seller", "hash", UserRole.Seller);
        var bidder = User.Create("bidder@takeauction.test", "Demo Bidder", "hash", UserRole.Bidder);

        var auction = Auction.Create(
            seller.Id,
            "Rare stamp collection",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now,
            TestHarness.Now.AddDays(2),
            TestHarness.Now);

        context.Users.AddRange(seller, bidder);
        context.Auctions.Add(auction);
        context.SaveChanges();

        _sellerId = seller.Id;
        _bidderId = bidder.Id;
        _auctionId = auction.Id;
    }
}
