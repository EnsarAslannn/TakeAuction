using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

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
