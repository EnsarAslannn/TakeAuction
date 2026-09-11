using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Watchlist.WatchAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Watchlist;

public sealed class WatchAuctionHandlerTests : IDisposable
{
    private static readonly Guid WatcherId = Guid.CreateVersion7();

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly FixedTimeProvider _clock = new(TestHarness.Now);

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Puts_an_open_lot_on_the_watchlist()
    {
        var auction = await SeedAsync();

        var result = await Handle(auction.Id);

        Assert.Equal(WatchAuctionResult.Watching, result);

        var watch = await _dbContext.AuctionWatches.AsNoTracking().SingleAsync();
        Assert.Equal(WatcherId, watch.UserId);
        Assert.Equal(auction.Id, watch.AuctionId);
        Assert.Equal(TestHarness.Now, watch.CreatedAtUtc);
    }

    [Fact]
    public async Task Accepts_a_lot_that_has_not_opened_yet()
    {
        var auction = await SeedAsync(startsIn: TimeSpan.FromHours(1));

        Assert.Equal(AuctionStatus.Scheduled, auction.Status);
        Assert.Equal(WatchAuctionResult.Watching, await Handle(auction.Id));
    }

    [Fact]
    public async Task Watching_twice_keeps_one_row_and_the_first_timestamp()
    {
        var auction = await SeedAsync();

        await Handle(auction.Id);
        _clock.Advance(TimeSpan.FromMinutes(10));
        var second = await Handle(auction.Id);

        Assert.Equal(WatchAuctionResult.Watching, second);

        var watch = await _dbContext.AuctionWatches.AsNoTracking().SingleAsync();
        Assert.Equal(TestHarness.Now, watch.CreatedAtUtc);
    }

    [Fact]
    public async Task Reports_a_lot_that_does_not_exist()
    {
        Assert.Equal(WatchAuctionResult.AuctionNotFound, await Handle(Guid.CreateVersion7()));
        Assert.Empty(await _dbContext.AuctionWatches.ToListAsync());
    }

    [Fact]
    public async Task Refuses_a_lot_that_has_ended()
    {
        var auction = await SeedAsync();

        _dbContext.Attach(auction);
        auction.End(auction.EndsAtUtc);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(WatchAuctionResult.AuctionClosed, await Handle(auction.Id));
        Assert.Empty(await _dbContext.AuctionWatches.ToListAsync());
    }

    [Fact]
    public async Task Refuses_a_lot_that_was_withdrawn()
    {
        var auction = await SeedAsync();

        _dbContext.Attach(auction);
        auction.Cancel(auction.SellerId, TestHarness.Now);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        Assert.Equal(WatchAuctionResult.AuctionClosed, await Handle(auction.Id));
    }

    private Task<WatchAuctionResult> Handle(Guid auctionId) =>
        new WatchAuctionHandler(_dbContext, _clock)
            .Handle(new WatchAuctionCommand(WatcherId, auctionId), CancellationToken.None);

    private async Task<Auction> SeedAsync(TimeSpan? startsIn = null)
    {
        var startsAt = TestHarness.Now.Add(startsIn ?? TimeSpan.FromMinutes(-10));

        var auction = Auction.Create(
            Guid.CreateVersion7(),
            "Rare stamp collection",
            "A lot under test.",
            100m,
            5m,
            startsAt,
            startsAt.AddDays(1),
            TestHarness.Now);

        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return auction;
    }
}
