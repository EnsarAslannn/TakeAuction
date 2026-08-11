using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.GetAuctionById;

public sealed class GetAuctionByIdHandler : IRequestHandler<GetAuctionByIdQuery, AuctionDetailResponse?>
{
    private readonly AppDbContext _dbContext;
    private readonly ICacheService _cache;
    private readonly AuctionCache _auctionCache;

    public GetAuctionByIdHandler(AppDbContext dbContext, ICacheService cache, AuctionCache auctionCache)
    {
        _dbContext = dbContext;
        _cache = cache;
        _auctionCache = auctionCache;
    }

    public Task<AuctionDetailResponse?> Handle(GetAuctionByIdQuery query, CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(
            AuctionCache.DetailKey(query.AuctionId),
            _auctionCache.DetailTtl,
            token => LoadAsync(query.AuctionId, token),
            cancellationToken);

    private async Task<AuctionDetailResponse?> LoadAsync(Guid auctionId, CancellationToken cancellationToken)
    {
        var row = await (
            from auction in _dbContext.Auctions.AsNoTracking()
            join seller in _dbContext.Users.AsNoTracking() on auction.SellerId equals seller.Id
            where auction.Id == auctionId
            select new
            {
                auction.Id,
                auction.Title,
                auction.Description,
                auction.StartingPrice,
                auction.CurrentPrice,
                auction.MinimumBidIncrement,
                auction.BidCount,
                auction.Status,
                auction.StartsAtUtc,
                auction.EndsAtUtc,
                auction.CreatedAtUtc,
                auction.SellerId,
                SellerDisplayName = seller.DisplayName
            }).SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new AuctionDetailResponse(
            row.Id,
            row.Title,
            row.Description,
            row.StartingPrice,
            row.CurrentPrice,
            row.MinimumBidIncrement,
            row.BidCount == 0 ? row.StartingPrice : row.CurrentPrice + row.MinimumBidIncrement,
            row.BidCount,
            row.Status.ToString(),
            row.StartsAtUtc,
            row.EndsAtUtc,
            row.CreatedAtUtc,
            row.SellerId,
            row.SellerDisplayName);
    }
}
