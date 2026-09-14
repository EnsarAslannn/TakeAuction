using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auctions.GetBidStanding;

public sealed class GetBidStandingHandler : IRequestHandler<GetBidStandingQuery, BidStandingResponse?>
{
    private readonly AppDbContext _dbContext;

    public GetBidStandingHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<BidStandingResponse?> Handle(GetBidStandingQuery query, CancellationToken cancellationToken)
    {
        var auction = await _dbContext.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == query.AuctionId, cancellationToken);

        if (auction is null)
        {
            return null;
        }

        var isLeading = auction.LeadingBidderId == query.BidderId;

        return new BidStandingResponse(
            auction.Id,
            isLeading,
            isLeading ? auction.LeadingMaxAmount : null,
            auction.MinimumAcceptableBidFor(query.BidderId));
    }
}
