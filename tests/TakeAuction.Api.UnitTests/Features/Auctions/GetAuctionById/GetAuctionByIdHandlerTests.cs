using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.GetAuctionById;

public sealed class GetAuctionByIdHandlerTests : IDisposable
{
    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly ICacheService _cache = TestHarness.CreateCacheService();
    private readonly AuctionCache _auctionCache;
    private readonly GetAuctionByIdHandler _handler;

    public GetAuctionByIdHandlerTests()
    {
        _auctionCache = TestHarness.CreateAuctionCache(_cache);
        _handler = new GetAuctionByIdHandler(_dbContext, _cache, _auctionCache);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_auction()
    {
        var result = await _handler.Handle(new GetAuctionByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_the_auction_with_the_seller_display_name()
    {
        var (auctionId, _) = await SeedAsync();

        var result = await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(auctionId, result.Id);
        Assert.Equal("Rare stamp collection", result.Title);
        Assert.Equal("Demo Seller", result.SellerDisplayName);
        Assert.Equal(nameof(AuctionStatus.Active), result.Status);
        Assert.Equal(100m, result.StartingPrice);
        Assert.Equal(100m, result.CurrentPrice);
        Assert.Equal(5m, result.MinimumBidIncrement);
    }

    [Fact]
    public async Task Writes_the_auction_into_the_cache_on_a_miss()
    {
        var (auctionId, _) = await SeedAsync();

        await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        var cached = await _cache.GetAsync<AuctionDetailResponse>(
            AuctionCache.DetailKey(auctionId),
            CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal(auctionId, cached.Id);
    }

    [Fact]
    public async Task Second_call_is_served_from_the_cache()
    {
        var (auctionId, _) = await SeedAsync();

        await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        _dbContext.Auctions.RemoveRange(await _dbContext.Auctions.ToListAsync());
        await _dbContext.SaveChangesAsync();

        var second = await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(auctionId, second.Id);
    }

    [Fact]
    public async Task Invalidating_the_detail_entry_forces_a_fresh_read()
    {
        var (auctionId, _) = await SeedAsync();

        await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        _dbContext.Auctions.RemoveRange(await _dbContext.Auctions.ToListAsync());
        await _dbContext.SaveChangesAsync();
        await _auctionCache.InvalidateDetailAsync(auctionId, CancellationToken.None);

        var second = await _handler.Handle(new GetAuctionByIdQuery(auctionId), CancellationToken.None);

        Assert.Null(second);
    }

    [Fact]
    public async Task Missing_auctions_are_not_negatively_cached()
    {
        var unknownId = Guid.CreateVersion7();

        await _handler.Handle(new GetAuctionByIdQuery(unknownId), CancellationToken.None);

        var cached = await _cache.GetAsync<AuctionDetailResponse>(
            AuctionCache.DetailKey(unknownId),
            CancellationToken.None);

        Assert.Null(cached);
    }

    public void Dispose() => _dbContext.Dispose();

    private async Task<(Guid AuctionId, Guid SellerId)> SeedAsync()
    {
        var seller = User.Create("seller@takeauction.local", "Demo Seller", "hash", UserRole.Seller);

        var auction = Auction.Create(
            seller.Id,
            "Rare stamp collection",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now,
            TestHarness.Now.AddDays(2),
            TestHarness.Now);

        _dbContext.Users.Add(seller);
        _dbContext.Auctions.Add(auction);
        await _dbContext.SaveChangesAsync();

        return (auction.Id, seller.Id);
    }
}
