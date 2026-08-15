using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.CancelAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.CancelAuction;

public sealed class CancelAuctionHandlerTests : IDisposable
{
    private readonly string _databaseName = $"cancelauction-{Guid.CreateVersion7()}";
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly List<AppDbContext> _contexts = [];

    private Guid _auctionId;
    private Guid _sellerId;
    private Guid _bidderId;

    public CancelAuctionHandlerTests() => Seed();

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    [Fact]
    public async Task Withdraws_a_lot_nobody_has_bid_on()
    {
        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(nameof(AuctionStatus.Cancelled), result.Response!.Status);
        Assert.Equal(TestHarness.Now, result.Response.CancelledAtUtc);

        await using var verification = NewContext();
        Assert.Equal(AuctionStatus.Cancelled, (await verification.Auctions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Queues_the_withdrawal_in_the_same_save_as_the_status_change()
    {
        await CreateHandler().Handle(Command(), CancellationToken.None);

        await using var verification = NewContext();
        var message = await verification.OutboxMessages.SingleAsync();

        Assert.Equal(nameof(AuctionCancelledIntegrationEvent), message.Type);
        Assert.Null(message.ProcessedAtUtc);
    }

    [Fact]
    public async Task Announces_the_withdrawal_so_the_cache_can_let_go_of_it()
    {
        await CreateHandler().Handle(Command(), CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<AuctionCancelledEvent>(domainEvent =>
                domainEvent.AuctionId == _auctionId
                && domainEvent.SellerId == _sellerId
                && domainEvent.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reports_an_unknown_auction()
    {
        var result = await CreateHandler().Handle(
            new CancelAuctionCommand(Guid.CreateVersion7(), _sellerId),
            CancellationToken.None);

        Assert.Equal(CancelRejection.AuctionNotFound, result.Rejection);
    }

    [Fact]
    public async Task Refuses_a_seller_who_does_not_own_the_lot()
    {
        var stranger = AddUser(UserRole.Seller);

        var result = await CreateHandler().Handle(
            new CancelAuctionCommand(_auctionId, stranger),
            CancellationToken.None);

        Assert.Equal(CancelRejection.NotTheSeller, result.Rejection);
        await AssertStillStandingAsync();
    }

    [Fact]
    public async Task Refuses_a_lot_that_has_already_been_bid_on()
    {
        PlaceRivalBid();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        Assert.Equal(CancelRejection.AlreadyBidOn, result.Rejection);
        await AssertStillStandingAsync();
    }

    [Fact]
    public async Task Leaves_nothing_queued_when_the_withdrawal_is_refused()
    {
        PlaceRivalBid();

        await CreateHandler().Handle(Command(), CancellationToken.None);

        await using var verification = NewContext();

        Assert.Empty(await verification.OutboxMessages
            .Where(message => message.Type == nameof(AuctionCancelledIntegrationEvent))
            .ToListAsync());
    }

    [Fact]
    public async Task A_bid_that_lands_mid_withdrawal_wins_and_the_lot_stays_up()
    {
        // The conflict stands in for a bid arriving between the read and the write. On the
        // retry the rule sees it and refuses, which is the answer the seller needs.
        var interceptor = new ConcurrencyConflictInterceptor(
            conflictCount: 1,
            onConflict: PlaceRivalBid);

        var result = await CreateHandler(interceptor).Handle(Command(), CancellationToken.None);

        Assert.Equal(CancelRejection.AlreadyBidOn, result.Rejection);
        await AssertStillStandingAsync();
    }

    [Fact]
    public async Task Gives_up_after_the_attempt_budget_is_exhausted()
    {
        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: int.MaxValue);

        var result = await CreateHandler(interceptor).Handle(Command(), CancellationToken.None);

        Assert.Equal(CancelRejection.ConcurrencyConflict, result.Rejection);
        Assert.Equal(CancelAuctionHandler.MaxAttempts, interceptor.SaveAttempts);
        await AssertStillStandingAsync();
    }

    private async Task AssertStillStandingAsync()
    {
        await using var verification = NewContext();

        Assert.NotEqual(AuctionStatus.Cancelled, (await verification.Auctions.SingleAsync()).Status);
        await _publisher.DidNotReceive().Publish(
            Arg.Any<AuctionCancelledEvent>(),
            Arg.Any<CancellationToken>());
    }

    private CancelAuctionCommand Command() => new(_auctionId, _sellerId);

    private CancelAuctionHandler CreateHandler(params IInterceptor[] interceptors)
    {
        var dbContext = NewContext(interceptors);

        return new CancelAuctionHandler(
            dbContext,
            _timeProvider,
            _publisher,
            TestHarness.CreateOutbox(dbContext),
            NullLogger<CancelAuctionHandler>.Instance);
    }

    private AppDbContext NewContext(params IInterceptor[] interceptors)
    {
        var context = TestHarness.CreateDbContext(_databaseName, interceptors);
        _contexts.Add(context);

        return context;
    }

    private void PlaceRivalBid()
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var auction = context.Auctions.Single(entity => entity.Id == _auctionId);
        Assert.True(auction.PlaceBid(_bidderId, 150m, TestHarness.Now).Succeeded);

        context.SaveChanges();
    }

    private Guid AddUser(UserRole role)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var user = User.Create($"{Guid.CreateVersion7():N}@takeauction.test", "Someone", "hash", role);
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
