namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record AuctionCreatedIntegrationEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal StartingPrice,
    string Status,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
