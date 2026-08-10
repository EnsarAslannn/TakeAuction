namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record BidPlacedIntegrationEvent(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    decimal PreviousPrice,
    Guid? OutbidBidderId,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
