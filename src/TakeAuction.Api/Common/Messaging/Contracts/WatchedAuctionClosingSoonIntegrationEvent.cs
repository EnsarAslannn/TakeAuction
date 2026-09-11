namespace TakeAuction.Api.Common.Messaging.Contracts;

public sealed record WatchedAuctionClosingSoonIntegrationEvent(
    Guid UserId,
    Guid AuctionId,
    string AuctionTitle,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
