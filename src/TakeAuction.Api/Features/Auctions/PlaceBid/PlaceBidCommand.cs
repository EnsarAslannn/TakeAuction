using MediatR;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

public sealed record PlaceBidCommand(
    Guid AuctionId,
    Guid BidderId,
    decimal Amount,
    string? IdempotencyKey = null) : IRequest<PlaceBidResult>;

public sealed record PlaceBidRequest(decimal Amount);

public sealed record PlaceBidResponse(
    Guid BidId,
    Guid AuctionId,
    decimal Amount,
    decimal MaxAmount,
    decimal CurrentPrice,
    decimal MinimumNextBid,
    int BidCount,
    bool IsLeading,
    bool AnsweredByProxy,
    DateTimeOffset PlacedAtUtc,
    DateTimeOffset EndsAtUtc,
    bool AuctionExtended);

public sealed record PlaceBidResult(
    BidRejection Rejection,
    PlaceBidResponse? Response,
    decimal? MinimumAcceptableBid,
    bool Replayed = false)
{
    public bool Succeeded => Rejection == BidRejection.None;

    public static PlaceBidResult Accepted(PlaceBidResponse response) =>
        new(BidRejection.None, response, null);

    public static PlaceBidResult Replay(PlaceBidResponse response) =>
        new(BidRejection.None, response, null, Replayed: true);

    public static PlaceBidResult Rejected(BidRejection rejection, decimal? minimumAcceptableBid = null) =>
        new(rejection, null, minimumAcceptableBid);
}
