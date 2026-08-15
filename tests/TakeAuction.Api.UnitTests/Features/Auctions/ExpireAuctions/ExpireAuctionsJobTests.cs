using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.ExpireAuctions;

public sealed class ExpireAuctionsJobTests : IDisposable
{
    private readonly string _databaseName = $"expire-auctions-{Guid.CreateVersion7()}";
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly List<AppDbContext> _contexts = [];

    private Guid _sellerId;

    public ExpireAuctionsJobTests() => Seed();

    [Fact]
    public async Task Closes_an_auction_whose_end_time_has_passed()
    {
        var auctionId = AddAuction(endsIn: TimeSpan.FromHours(-1));

        var closed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(1, closed);
        Assert.Equal(AuctionStatus.Ended, await StatusOfAsync(auctionId));
    }

    [Fact]
    public async Task Leaves_a_running_auction_open()
    {
        var auctionId = AddAuction(endsIn: TimeSpan.FromHours(1));

        var closed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(0, closed);
        Assert.Equal(AuctionStatus.Active, await StatusOfAsync(auctionId));
        await _publisher.DidNotReceive().Publish(Arg.Any<AuctionEndedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Is_idempotent_across_sweeps()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-1));
        var job = CreateJob();

        Assert.Equal(1, await job.RunAsync(CancellationToken.None));
        Assert.Equal(0, await job.RunAsync(CancellationToken.None));

        await _publisher.Received(1).Publish(Arg.Any<AuctionEndedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closes_every_due_auction_in_one_sweep()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-3));
        AddAuction(endsIn: TimeSpan.FromHours(-2));
        AddAuction(endsIn: TimeSpan.FromHours(-1));
        AddAuction(endsIn: TimeSpan.FromHours(4));

        var closed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(3, closed);
        await _publisher.Received(3).Publish(Arg.Any<AuctionEndedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_closes_more_than_the_configured_batch_size()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-3));
        AddAuction(endsIn: TimeSpan.FromHours(-2));
        AddAuction(endsIn: TimeSpan.FromHours(-1));

        var closed = await CreateJob(batchSize: 2).RunAsync(CancellationToken.None);

        Assert.Equal(2, closed);
    }

    [Fact]
    public async Task Takes_the_longest_overdue_auctions_first()
    {
        var oldest = AddAuction(endsIn: TimeSpan.FromHours(-5));
        AddAuction(endsIn: TimeSpan.FromHours(-1));

        await CreateJob(batchSize: 1).RunAsync(CancellationToken.None);

        Assert.Equal(AuctionStatus.Ended, await StatusOfAsync(oldest));
    }

    [Fact]
    public async Task Announces_the_winner_and_the_final_price()
    {
        var bidderId = AddUser(UserRole.Bidder);
        var auctionId = AddAuction(endsIn: TimeSpan.FromHours(-1), winningBid: (bidderId, 250m));

        await CreateJob().RunAsync(CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<AuctionEndedEvent>(domainEvent =>
                domainEvent.AuctionId == auctionId
                && domainEvent.SellerId == _sellerId
                && domainEvent.WinningBidderId == bidderId
                && domainEvent.FinalPrice == 250m
                && domainEvent.BidCount == 1
                && domainEvent.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Announces_an_auction_that_drew_no_bids_without_a_winner()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-1));

        await CreateJob().RunAsync(CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<AuctionEndedEvent>(domainEvent =>
                domainEvent.WinningBidderId == null
                && domainEvent.BidCount == 0
                && domainEvent.FinalPrice == 100m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Closes_a_scheduled_auction_whose_window_elapsed_without_ever_starting()
    {
        var auctionId = AddAuction(startsIn: TimeSpan.FromHours(-2), endsIn: TimeSpan.FromHours(-1), scheduled: true);

        var closed = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(1, closed);
        Assert.Equal(AuctionStatus.Ended, await StatusOfAsync(auctionId));
    }

    [Fact]
    public async Task Skips_an_auction_that_lost_a_race_and_leaves_it_for_the_next_sweep()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-2));
        AddAuction(endsIn: TimeSpan.FromHours(-1));

        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: 1);

        var closed = await CreateJob(interceptors: interceptor).RunAsync(CancellationToken.None);

        Assert.Equal(1, closed);
        await _publisher.Received(1).Publish(Arg.Any<AuctionEndedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publishes_nothing_when_the_write_never_lands()
    {
        AddAuction(endsIn: TimeSpan.FromHours(-1));

        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: int.MaxValue);

        var closed = await CreateJob(interceptors: interceptor).RunAsync(CancellationToken.None);

        Assert.Equal(0, closed);
        await _publisher.DidNotReceive().Publish(Arg.Any<AuctionEndedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_no_work_when_nothing_is_due()
    {
        AddAuction(endsIn: TimeSpan.FromHours(6));

        Assert.Equal(0, await CreateJob().RunAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    private ExpireAuctionsJob CreateJob(int batchSize = 200, params IInterceptor[] interceptors)
    {
        var options = Options.Create(new JobOptions { ExpireAuctionsBatchSize = batchSize });
        var dbContext = NewContext(interceptors);

        return new ExpireAuctionsJob(
            dbContext,
            _timeProvider,
            _publisher,
            TestHarness.CreateOutbox(dbContext),
            options,
            NullLogger<ExpireAuctionsJob>.Instance);
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
        context.Users.Add(seller);
        context.SaveChanges();

        _sellerId = seller.Id;
    }

    private Guid AddUser(UserRole role)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var user = User.Create($"{Guid.CreateVersion7():N}@takeauction.test", "Demo Bidder", "hash", role);
        context.Users.Add(user);
        context.SaveChanges();

        return user.Id;
    }

    private Guid AddAuction(
        TimeSpan? endsIn = null,
        TimeSpan? startsIn = null,
        bool scheduled = false,
        (Guid BidderId, decimal Amount)? winningBid = null)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var startsAt = TestHarness.Now.Add(startsIn ?? TimeSpan.FromHours(-6));
        var endsAt = TestHarness.Now.Add(endsIn ?? TimeSpan.FromHours(-1));

        var auction = Auction.Create(
            _sellerId,
            "Rare stamp collection",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            startsAt,
            endsAt,
            scheduled ? startsAt.AddHours(-1) : startsAt);

        if (winningBid is { } bid)
        {
            var outcome = auction.PlaceBid(bid.BidderId, bid.Amount, startsAt);
            Assert.True(outcome.Succeeded);
        }

        context.Auctions.Add(auction);
        context.SaveChanges();

        return auction.Id;
    }

    private async Task<AuctionStatus> StatusOfAsync(Guid auctionId)
    {
        await using var context = TestHarness.CreateDbContext(_databaseName);

        return await context.Auctions
            .Where(auction => auction.Id == auctionId)
            .Select(auction => auction.Status)
            .SingleAsync();
    }
}
