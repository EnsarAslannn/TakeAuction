using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.GetAuctions;

public sealed class GetAuctionsHandler : IRequestHandler<GetAuctionsQuery, PagedResult<AuctionListItem>>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly AuctionCache _auctionCache;

    public GetAuctionsHandler(AppDbContext dbContext, ICacheService cache, AuctionCache auctionCache)
    {
        _dbContext = dbContext;
        _cache = cache;
        _auctionCache = auctionCache;
    }

    public async Task<PagedResult<AuctionListItem>> Handle(
        GetAuctionsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query.Normalize();

        var generation = await _auctionCache.GetListGenerationAsync(cancellationToken);
        var cacheKey = AuctionCache.ListKey(
            generation,
            normalized.Page,
            normalized.PageSize,
            normalized.Status,
            normalized.SellerId,
            normalized.Search);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            _auctionCache.ListTtl,
            token => LoadPageAsync(normalized, token),
            cancellationToken);
    }

    private async Task<PagedResult<AuctionListItem>> LoadPageAsync(
        GetAuctionsQuery query,
        CancellationToken cancellationToken)
    {
        var auctions = _dbContext.Auctions.AsNoTracking();

        if (query.Status is { } status)
        {
            auctions = auctions.Where(auction => auction.Status == status);
        }

        if (query.SellerId is { } sellerId)
        {
            auctions = auctions.Where(auction => auction.SellerId == sellerId);
        }

        if (query.Search is { } search)
        {
            var pattern = search.ToLower();
            auctions = auctions.Where(auction => auction.Title.ToLower().Contains(pattern));
        }

        var totalCount = await auctions.CountAsync(cancellationToken);

        var rows = await auctions
            .OrderByDescending(auction => auction.CreatedAtUtc)
            .ThenByDescending(auction => auction.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(auction => new
            {
                auction.Id,
                auction.Title,
                auction.StartingPrice,
                auction.CurrentPrice,
                auction.Status,
                auction.StartsAtUtc,
                auction.EndsAtUtc,
                auction.SellerId
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AuctionListItem(
                row.Id,
                row.Title,
                row.StartingPrice,
                row.CurrentPrice,
                row.Status.ToString(),
                row.StartsAtUtc,
                row.EndsAtUtc,
                row.SellerId))
            .ToList();

        return new PagedResult<AuctionListItem>(items, query.Page, query.PageSize, totalCount);
    }
}
