using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.GetAuctions;

public sealed class GetAuctionsHandlerTests : IDisposable
{
    private static readonly Guid SellerA = Guid.CreateVersion7();
    private static readonly Guid SellerB = Guid.CreateVersion7();

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly GetAuctionsHandler _handler;

    public GetAuctionsHandlerTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new GetAuctionsHandler(_dbContext, _cache, _auctionCache);
    }

    [Fact]
    public async Task Returns_every_auction_on_the_first_page()
    {
        await SeedAsync();

        var result = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(4, result.Items.Count);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task Pages_the_result_set()
    {
        await SeedAsync();

        var firstPage = await _handler.Handle(new GetAuctionsQuery(1, 2), CancellationToken.None);
        var secondPage = await _handler.Handle(new GetAuctionsQuery(2, 2), CancellationToken.None);

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal(2, secondPage.Items.Count);
        Assert.Equal(4, firstPage.TotalCount);
        Assert.Equal(2, firstPage.TotalPages);
        Assert.True(firstPage.HasNextPage);
        Assert.False(secondPage.HasNextPage);
        Assert.Empty(firstPage.Items.Select(item => item.Id).Intersect(secondPage.Items.Select(item => item.Id)));
    }

    [Fact]
    public async Task Orders_newest_first()
    {
        await SeedAsync();

        var result = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        Assert.Equal("Newest lot", result.Items[0].Title);
    }

    [Fact]
    public async Task Filters_by_status()
    {
        await SeedAsync();

        var result = await _handler.Handle(
            new GetAuctionsQuery(Status: AuctionStatus.Scheduled),
            CancellationToken.None);

        Assert.All(result.Items, item => Assert.Equal(nameof(AuctionStatus.Scheduled), item.Status));
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task Filters_by_seller()
    {
        await SeedAsync();

        var result = await _handler.Handle(new GetAuctionsQuery(SellerId: SellerB), CancellationToken.None);

        Assert.All(result.Items, item => Assert.Equal(SellerB, item.SellerId));
        Assert.NotEmpty(result.Items);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await SeedAsync();

        var result = await _handler.Handle(new GetAuctionsQuery(Search: "  RARE  "), CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Rare stamp collection", item.Title);
    }

    [Fact]
    public async Task Clamps_out_of_range_paging_values()
    {
        await SeedAsync();

        var result = await _handler.Handle(new GetAuctionsQuery(-5, 5_000), CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(GetAuctionsQuery.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task Second_call_is_served_from_the_cache()
    {
        await SeedAsync();

        var first = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        _dbContext.Auctions.Add(NewAuction(SellerA, "Added after the cache was warmed", TestHarness.Now.AddMinutes(5)));
        await _dbContext.SaveChangesAsync();

        var second = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.DoesNotContain(second.Items, item => item.Title == "Added after the cache was warmed");
    }

    [Fact]
    public async Task Rotating_the_generation_forces_a_fresh_read()
    {
        await SeedAsync();

        var first = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        _dbContext.Auctions.Add(NewAuction(SellerA, "Added after the cache was warmed", TestHarness.Now.AddMinutes(5)));
        await _dbContext.SaveChangesAsync();
        await _auctionCache.InvalidateListsAsync(CancellationToken.None);

        var second = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);

        Assert.Equal(first.TotalCount + 1, second.TotalCount);
        Assert.Contains(second.Items, item => item.Title == "Added after the cache was warmed");
    }

    [Fact]
    public async Task Different_filters_do_not_share_a_cache_entry()
    {
        await SeedAsync();

        var all = await _handler.Handle(new GetAuctionsQuery(), CancellationToken.None);
        var sellerB = await _handler.Handle(new GetAuctionsQuery(SellerId: SellerB), CancellationToken.None);

        Assert.NotEqual(all.TotalCount, sellerB.TotalCount);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task SeedAsync()
    {
        _dbContext.Auctions.AddRange(
            NewAuction(SellerA, "Rare stamp collection", TestHarness.Now.AddMinutes(-40)),
            NewAuction(SellerA, "Antique oak desk", TestHarness.Now.AddMinutes(-30), startsInFuture: true),
            NewAuction(SellerB, "Signed first edition", TestHarness.Now.AddMinutes(-20), startsInFuture: true),
            NewAuction(SellerB, "Newest lot", TestHarness.Now.AddMinutes(-10)));

        await _dbContext.SaveChangesAsync();
    }

    private static Auction NewAuction(
        Guid sellerId,
        string title,
        DateTimeOffset createdAtUtc,
        bool startsInFuture = false) =>
        Auction.Create(
            sellerId,
            title,
            "A detailed description of the lot on offer.",
            100m,
            5m,
            startsInFuture ? createdAtUtc.AddHours(6) : createdAtUtc,
            createdAtUtc.AddDays(2),
            createdAtUtc);
}
