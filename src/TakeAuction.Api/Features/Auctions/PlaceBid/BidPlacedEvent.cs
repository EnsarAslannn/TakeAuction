using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

public sealed record BidPlacedEvent(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    decimal PreviousPrice,
    Guid? OutbidBidderId,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
