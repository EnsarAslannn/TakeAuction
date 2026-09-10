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
using TakeAuction.Api.Features.Auctions.OpenAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.OpenAuctions;

public sealed class ActivateAuctionsJobTests : IDisposable
{
    private readonly string _databaseName = $"activate-auctions-{Guid.CreateVersion7()}";
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();
    private readonly List<AppDbContext> _contexts = [];

    private Guid _sellerId;

    public ActivateAuctionsJobTests() => Seed();

    [Fact]
    public async Task Opens_a_scheduled_auction_whose_start_time_has_passed()
    {
        var auctionId = AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        var opened = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(1, opened);
        Assert.Equal(AuctionStatus.Active, await StatusOfAsync(auctionId));
    }

    [Fact]
    public async Task Leaves_an_auction_that_has_not_started_alone()
    {
        var auctionId = AddScheduledAuction(startsIn: TimeSpan.FromHours(1));

        var opened = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(0, opened);
        Assert.Equal(AuctionStatus.Scheduled, await StatusOfAsync(auctionId));
        await _publisher.DidNotReceive().Publish(Arg.Any<AuctionOpenedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaves_a_lot_whose_whole_window_elapsed_to_the_closing_sweep()
    {
        var auctionId = AddScheduledAuction(startsIn: TimeSpan.FromHours(-3), endsIn: TimeSpan.FromHours(-1));

        var opened = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(0, opened);
        Assert.Equal(AuctionStatus.Scheduled, await StatusOfAsync(auctionId));
    }

    [Fact]
    public async Task Is_idempotent_across_sweeps()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));
        var job = CreateJob();

        Assert.Equal(1, await job.RunAsync(CancellationToken.None));
        Assert.Equal(0, await job.RunAsync(CancellationToken.None));

        await _publisher.Received(1).Publish(Arg.Any<AuctionOpenedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Opens_every_due_auction_in_one_sweep()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-3));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-2));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(4));

        var opened = await CreateJob().RunAsync(CancellationToken.None);

        Assert.Equal(3, opened);
        await _publisher.Received(3).Publish(Arg.Any<AuctionOpenedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Never_opens_more_than_the_configured_batch_size()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-3));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-2));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        var opened = await CreateJob(batchSize: 2).RunAsync(CancellationToken.None);

        Assert.Equal(2, opened);
    }

    [Fact]
    public async Task Takes_the_longest_overdue_auctions_first()
    {
        var oldest = AddScheduledAuction(startsIn: TimeSpan.FromHours(-5));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        await CreateJob(batchSize: 1).RunAsync(CancellationToken.None);

        Assert.Equal(AuctionStatus.Active, await StatusOfAsync(oldest));
    }

    [Fact]
    public async Task Announces_the_lot_at_its_starting_price()
    {
        var auctionId = AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        await CreateJob().RunAsync(CancellationToken.None);

        await _publisher.Received(1).Publish(
            Arg.Is<AuctionOpenedEvent>(domainEvent =>
                domainEvent.AuctionId == auctionId
                && domainEvent.SellerId == _sellerId
                && domainEvent.CurrentPrice == 100m
                && domainEvent.OccurredAtUtc == TestHarness.Now),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_an_auction_that_lost_a_race_and_leaves_it_for_the_next_sweep()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-2));
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: 1);

        var opened = await CreateJob(interceptors: interceptor).RunAsync(CancellationToken.None);

        Assert.Equal(1, opened);
        await _publisher.Received(1).Publish(Arg.Any<AuctionOpenedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publishes_nothing_when_the_write_never_lands()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(-1));

        var interceptor = new ConcurrencyConflictInterceptor(conflictCount: int.MaxValue);

        var opened = await CreateJob(interceptors: interceptor).RunAsync(CancellationToken.None);

        Assert.Equal(0, opened);
        await _publisher.DidNotReceive().Publish(Arg.Any<AuctionOpenedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Does_no_work_when_nothing_is_due()
    {
        AddScheduledAuction(startsIn: TimeSpan.FromHours(6));

        Assert.Equal(0, await CreateJob().RunAsync(CancellationToken.None));
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
        {
            context.Dispose();
        }
    }

    private ActivateAuctionsJob CreateJob(int batchSize = 200, params IInterceptor[] interceptors)
    {
        var options = Options.Create(new JobOptions { ActivateAuctionsBatchSize = batchSize });
        var dbContext = NewContext(interceptors);

        var opener = new AuctionOpener(
            dbContext,
            _timeProvider,
            _publisher,
            TestHarness.CreateOutbox(dbContext),
            NullLogger<AuctionOpener>.Instance);

        return new ActivateAuctionsJob(
            dbContext,
            opener,
            _timeProvider,
            options,
            NullLogger<ActivateAuctionsJob>.Instance);
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

    private Guid AddScheduledAuction(TimeSpan startsIn, TimeSpan? endsIn = null)
    {
        using var context = TestHarness.CreateDbContext(_databaseName);

        var startsAt = TestHarness.Now.Add(startsIn);
        var endsAt = TestHarness.Now.Add(endsIn ?? startsIn.Add(TimeSpan.FromHours(24)));

        var auction = Auction.Create(
            _sellerId,
            "Rare stamp collection",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            startsAt,
            endsAt,
            startsAt.AddHours(-1));

        Assert.Equal(AuctionStatus.Scheduled, auction.Status);

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
