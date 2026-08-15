using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

/// <summary>
/// Describes where the lot stands after a submission rather than the submission itself: with
/// proxies in play the bid that moved the price is often the leader's automatic answer, not the
/// one that was sent in. Watchers care about the price and who holds it.
/// </summary>
public sealed record BidPlacedEvent(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    bool Automatic,
    decimal PreviousPrice,
    Guid? OutbidBidderId,
    DateTimeOffset EndsAtUtc,
    bool AuctionExtended,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
