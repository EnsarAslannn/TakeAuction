using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.GetAuctionBids;

public sealed class GetAuctionBidsHandler
    : IRequestHandler<GetAuctionBidsQuery, PagedResult<AuctionBidItem>?>
{
    private readonly AppDbContext _dbContext;

    public GetAuctionBidsHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<PagedResult<AuctionBidItem>?> Handle(
        GetAuctionBidsQuery query,
        CancellationToken cancellationToken)
    {
        var normalized = query.Normalize();

        // An unknown auction and one that has drawn no bids are different answers, so the
        // caller gets a 404 rather than an empty page for a lot that does not exist.
        var auctionExists = await _dbContext.Auctions
            .AsNoTracking()
            .AnyAsync(auction => auction.Id == normalized.AuctionId, cancellationToken);

        if (!auctionExists)
        {
            return null;
        }

        var bids = _dbContext.Bids
            .AsNoTracking()
            .Where(bid => bid.AuctionId == normalized.AuctionId);

        var totalCount = await bids.CountAsync(cancellationToken);

        // Newest first: the page opens on the top of the ladder, which is what a bidder
        // arriving mid-auction wants to see.
        var items = await bids
            .OrderByDescending(bid => bid.Amount)
            .ThenByDescending(bid => bid.PlacedAtUtc)
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(bid => new AuctionBidItem(
                bid.Id,
                bid.Amount,
                bid.IsAutomatic,
                bid.PlacedAtUtc,
                bid.BidderId))
            .ToListAsync(cancellationToken);

        return new PagedResult<AuctionBidItem>(items, normalized.Page, normalized.PageSize, totalCount);
    }
}
