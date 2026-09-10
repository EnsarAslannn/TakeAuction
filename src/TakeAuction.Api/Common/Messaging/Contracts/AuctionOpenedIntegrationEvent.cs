namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record AuctionOpenedIntegrationEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal CurrentPrice,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
