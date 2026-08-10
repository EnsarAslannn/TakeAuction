namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record AuctionEndedIntegrationEvent(
    Guid AuctionId,
    Guid SellerId,
    Guid? WinningBidderId,
    decimal FinalPrice,
    int BidCount,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
