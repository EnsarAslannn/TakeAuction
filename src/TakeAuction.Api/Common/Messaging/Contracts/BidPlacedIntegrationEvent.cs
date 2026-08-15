namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record BidPlacedIntegrationEvent(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    bool Automatic,
    decimal PreviousPrice,
    Guid? OutbidBidderId,
    DateTimeOffset EndsAtUtc,
    bool AuctionExtended,
    DateTimeOffset OccurredAtUtc);
