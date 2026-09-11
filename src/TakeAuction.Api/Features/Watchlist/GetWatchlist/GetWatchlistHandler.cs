using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Watchlist.GetWatchlist;

public sealed class GetWatchlistHandler : IRequestHandler<GetWatchlistQuery, IReadOnlyList<WatchlistItem>>
{
    private readonly AppDbContext _dbContext;

    public GetWatchlistHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<WatchlistItem>> Handle(GetWatchlistQuery query, CancellationToken cancellationToken)
    {
        var items = await _dbContext.AuctionWatches
            .AsNoTracking()
            .Where(watch => watch.UserId == query.UserId)
            .Join(
                _dbContext.Auctions.AsNoTracking(),
                watch => watch.AuctionId,
                auction => auction.Id,
                (watch, auction) => new { watch, auction })
            .OrderBy(row => row.auction.Status == AuctionStatus.Ended || row.auction.Status == AuctionStatus.Cancelled)
            .ThenBy(row => row.auction.EndsAtUtc)
            .Take(GetWatchlistQuery.MaxItems)
            .Select(row => new WatchlistItem(
                row.auction.Id,
                row.auction.Title,
                row.auction.ImageUrl,
                row.auction.StartingPrice,
                row.auction.CurrentPrice,
                row.auction.Status.ToString(),
                row.auction.StartsAtUtc,
                row.auction.EndsAtUtc,
                row.auction.SellerId,
                row.watch.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return items;
    }
}
