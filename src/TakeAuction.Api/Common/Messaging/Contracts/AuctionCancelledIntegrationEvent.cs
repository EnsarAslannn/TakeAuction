namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record AuctionCancelledIntegrationEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal CurrentPrice,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
