using MediatR;

namespace TakeAuction.Api.Features.Auctions.GetBidStanding;

public sealed record GetBidStandingQuery(Guid AuctionId, Guid BidderId) : IRequest<BidStandingResponse?>;

public sealed record BidStandingResponse(
    Guid AuctionId,
    bool IsLeading,
    decimal? MaxAmount,
    decimal MinimumAcceptableBid);
