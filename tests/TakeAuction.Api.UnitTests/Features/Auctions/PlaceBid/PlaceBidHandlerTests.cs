using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.PlaceBid;

public sealed class PlaceBidHandlerTests : IDisposable
{
    private readonly string _databaseName = $"placebid-{Guid.CreateVersion7()}";
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly List<AppDbContext> _contexts = [];

    private Guid _auctionId;
    private Guid _sellerId;
    private Guid _bidderId;

    public PlaceBidHandlerTests() => Seed();

    [Fact]
    public async Task Accepts_a_valid_bid()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(150m, result.Response.Amount);
        Assert.Equal(150m, result.Response.CurrentPrice);
        Assert.Equal(155m, result.Response.MinimumNextBid);
        Assert.Equal(1, result.Response.BidCount);
    }

    [Fact]
    public async Task Persists_the_bid_and_the_new_auction_price()
    {
        var (handler, _) = CreateHandler();

        await handler.Handle(Command(150m), CancellationToken.None);

        await using var verification = NewContext();
        var bid = await verification.Bids.SingleAsync();
        var auction = await verification.Auctions.SingleAsync();

        Assert.Equal(_auctionId, bid.AuctionId);
        Assert.Equal(_bidderId, bid.BidderId);
        Assert.Equal(150m, bid.Amount);
        Assert.Equal(150m, auction.CurrentPrice);
        Assert.Equal(_bidderId, auction.LeadingBidderId);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public async Task Publishes_the_bid_placed_event()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<BidPlacedEvent>(domainEvent =>
                domainEvent.AuctionId == _auctionId
                && domainEvent.BidId == result.Response!.BidId
                && domainEvent.BidderId == _bidderId
                && domainEvent.Amount == 150m
                && domainEvent.PreviousPrice == 100m
                && domainEvent.OutbidBidderId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_the_outbid_leader_on_the_event()
    {
        var (handler, _) = CreateHandler();
        var firstBidder = _bidderId;
        await handler.Handle(Command(150m), CancellationToken.None);

        _bidderId = AddUser(UserRole.Bidder);
        await handler.Handle(Command(200m), CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<BidPlacedEvent>(domainEvent =>
                domainEvent.Amount == 200m
                && domainEvent.PreviousPrice == 150m
                && domainEvent.OutbidBidderId == firstBidder),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_an_unknown_auction()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(
            new PlaceBidCommand(Guid.CreateVersion7(), _bidderId, 150m),
            CancellationToken.None);

        Assert.Equal(BidRejection.AuctionNotFound, result.Rejection);
        await AssertNothingHappenedAsync();
    }

    [Fact]
    public async Task Rejects_the_seller()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(
            new PlaceBidCommand(_auctionId, _sellerId, 500m),
            CancellationToken.None);

        Assert.Equal(BidRejection.SellerCannotBid, result.Rejection);
        await AssertNothingHappenedAsync();
    }

    [Fact]
    public async Task Rejects_a_bid_below_the_minimum_and_reports_it()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(Command(99m), CancellationToken.None);

        Assert.Equal(BidRejection.BidTooLow, result.Rejection);
        Assert.Equal(100m, result.MinimumAcceptableBid);
        await AssertNothingHappenedAsync();
    }

    [Fact]
    public async Task Rejects_a_bid_on_an_auction_that_has_ended()
    {
        var (handler, _) = CreateHandler();
        _timeProvider.Advance(TimeSpan.FromDays(5));

        var result = await handler.Handle(Command(500m), CancellationToken.None);

        Assert.Equal(BidRejection.AuctionNotOpen, result.Rejection);
        await AssertNothingHappenedAsync();
    }

    [Fact]
    public async Task Retries_after_a_concurrency_conflict_and_succeeds()
    {
        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: 1);
        var (handler, _) = CreateHandler(interceptor);

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, interceptor.SaveAttempts);

        await using var verification = NewContext();
        Assert.Equal(1, await verification.Bids.CountAsync());
        Assert.Equal(150m, (await verification.Auctions.SingleAsync()).CurrentPrice);

        await _publisher.Received(1).Publish(Arg.Any<BidPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Gives_up_after_the_attempt_budget_is_exhausted()
    {
        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: int.MaxValue);
        var (handler, _) = CreateHandler(interceptor);

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        Assert.Equal(BidRejection.ConcurrencyConflict, result.Rejection);
        Assert.Equal(PlaceBidHandler.MaxAttempts, interceptor.SaveAttempts);
        await AssertNothingHappenedAsync();
    }

    [Fact]
    public async Task Re_evaluates_the_bid_against_the_price_that_won_the_race()
    {
        var interceptor = new ConcurrencyConflictInterceptor(
            conflictCount: 1,
            onConflict: () => RaiseWinningPrice(300m));

        var (handler, _) = CreateHandler(interceptor);

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        Assert.Equal(BidRejection.BidTooLow, result.Rejection);
        Assert.Equal(305m, result.MinimumAcceptableBid);

        await using var verification = NewContext();
        Assert.Empty(await verification.Bids.ToListAsync());
        await _publisher.DidNotReceive().Publish(Arg.Any<BidPlacedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accepts_the_retry_when_the_bid_still_clears_the_new_price()
    {
        var interceptor = new ConcurrencyConflictInterceptor(
            conflictCount: 1,
            onConflict: () => RaiseWinningPrice(120m));

        var (handler, _) = CreateHandler(interceptor);

        var result = await handler.Handle(Command(150m), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(150m, result.Response!.CurrentPrice);
        Assert.Equal(2, result.Response.BidCount);
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    private PlaceBidCommand Command(decimal amount) => new(_auctionId, _bidderId, amount);

    private (PlaceBidHandler Handler, AppDbContext DbContext) CreateHandler(params IInterceptor[] interceptors)
    {
        var dbContext = NewContext(interceptors);

        var handler = new PlaceBidHandler(
            dbContext,
            _timeProvider,
            _publisher,
            NullLogger<PlaceBidHandler>.Instance);

        return (handler, dbContext);
    }

    private AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var context = TestHarness.CreateDbContext(_databaseName, interceptors);
        _contexts.Add(context);

        return context;
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

    private Guid AddUser(UserRole role)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var user = User.Create($"{Guid.CreateVersion7():N}@takeauction.test", "Rival Bidder", "hash", role);
        context.Users.Add(user);
        context.SaveChanges();

        return user.Id;
    }

    private void RaiseWinningPrice(decimal amount)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var rivalId = AddUser(UserRole.Bidder);
        var auction = context.Auctions.Single(entity => entity.Id == _auctionId);
        auction.PlaceBid(rivalId, amount, TestHarness.Now);
        context.SaveChanges();
    }

    private async Task AssertNothingHappenedAsync()
    {
        await using var verification = NewContext();

        Assert.Empty(await verification.Bids.ToListAsync());
        Assert.Equal(100m, (await verification.Auctions.SingleAsync()).CurrentPrice);
        await _publisher.DidNotReceive().Publish(Arg.Any<BidPlacedEvent>(), Arg.Any<CancellationToken>());
    }
}
