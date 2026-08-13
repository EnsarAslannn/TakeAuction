using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctionBids;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.GetAuctionBids;

public sealed class GetAuctionBidsHandlerTests
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 10m;

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();

    [Fact]
    public async Task An_auction_that_does_not_exist_is_not_an_empty_page()
    {
        var result = await Handle(new GetAuctionBidsQuery(Guid.CreateVersion7()));

        Assert.Null(result);
    }

    [Fact]
    public async Task A_lot_with_no_bids_yet_returns_an_empty_page()
    {
        var auction = await AddAuctionAsync();

        var result = await Handle(new GetAuctionBidsQuery(auction.Id));

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task The_history_comes_back_highest_first()
    {
        var auction = await AddAuctionAsync();
        await AddBidsAsync(auction, 100m, 120m, 110m);

        var result = await Handle(new GetAuctionBidsQuery(auction.Id));

        Assert.NotNull(result);
        Assert.Equal([120m, 110m, 100m], result.Items.Select(item => item.Amount));
    }

    [Fact]
    public async Task Each_entry_carries_what_the_feed_renders()
    {
        var auction = await AddAuctionAsync();
        var bidderId = Guid.CreateVersion7();

        var bid = auction.PlaceBid(bidderId, StartingPrice, TestHarness.Now);
        _dbContext.Bids.Add(bid.Bid!);
        await _dbContext.SaveChangesAsync();

        var result = await Handle(new GetAuctionBidsQuery(auction.Id));

        var item = Assert.Single(result!.Items);

        Assert.Equal(bid.Bid!.Id, item.Id);
        Assert.Equal(StartingPrice, item.Amount);
        Assert.Equal(TestHarness.Now, item.PlacedAtUtc);
        Assert.Equal(bidderId, item.BidderId);
    }

    [Fact]
    public async Task Bids_on_other_lots_stay_out_of_the_history()
    {
        var auction = await AddAuctionAsync();
        var other = await AddAuctionAsync();

        await AddBidsAsync(auction, 100m, 110m);
        await AddBidsAsync(other, 100m);

        var result = await Handle(new GetAuctionBidsQuery(auction.Id));

        Assert.Equal(2, result!.TotalCount);
    }

    [Fact]
    public async Task Paging_walks_the_ladder_downwards()
    {
        var auction = await AddAuctionAsync();
        await AddBidsAsync(auction, 100m, 110m, 120m, 130m, 140m);

        var first = await Handle(new GetAuctionBidsQuery(auction.Id, Page: 1, PageSize: 2));
        var second = await Handle(new GetAuctionBidsQuery(auction.Id, Page: 2, PageSize: 2));
        var last = await Handle(new GetAuctionBidsQuery(auction.Id, Page: 3, PageSize: 2));

        Assert.Equal([140m, 130m], first!.Items.Select(item => item.Amount));
        Assert.Equal([120m, 110m], second!.Items.Select(item => item.Amount));
        Assert.Equal([100m], last!.Items.Select(item => item.Amount));

        Assert.Equal(5, first.TotalCount);
        Assert.True(first.HasNextPage);
        Assert.False(last.HasNextPage);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-5, 20)]
    public async Task A_page_below_one_is_pulled_back_to_the_first(int requestedPage, int expectedPageSize)
    {
        var auction = await AddAuctionAsync();
        await AddBidsAsync(auction, 100m);

        var result = await Handle(new GetAuctionBidsQuery(auction.Id, Page: requestedPage));

        Assert.Equal(1, result!.Page);
        Assert.Equal(expectedPageSize, result.PageSize);
    }

    [Fact]
    public async Task An_oversized_page_is_clamped_rather_than_refused()
    {
        var auction = await AddAuctionAsync();

        var result = await Handle(new GetAuctionBidsQuery(auction.Id, PageSize: 5_000));

        Assert.Equal(GetAuctionBidsQuery.MaxPageSize, result!.PageSize);
    }

    private Task<PagedResult<AuctionBidItem>?> Handle(GetAuctionBidsQuery query) =>
        new GetAuctionBidsHandler(_dbContext).Handle(query, CancellationToken.None);

    private async Task<Auction> AddAuctionAsync()
    {
        var auction = Auction.Create(
            Guid.CreateVersion7(),
            "Lot under test",
            "A lot used to exercise the bidding history.",
            StartingPrice,
            Increment,
            TestHarness.Now,
            TestHarness.Now.AddDays(1),
            TestHarness.Now);

        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        return auction;
    }

    private async Task AddBidsAsync(Auction auction, params decimal[] amounts)
    {
        var placedAt = TestHarness.Now;

        foreach (var amount in amounts.OrderBy(amount => amount))
        {
            placedAt = placedAt.AddSeconds(1);

            var outcome = auction.PlaceBid(Guid.CreateVersion7(), amount, placedAt);
            _dbContext.Bids.Add(outcome.Bid!);
        }

        await _dbContext.SaveChangesAsync();
    }
}
