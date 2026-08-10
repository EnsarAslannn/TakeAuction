namespace TakeAuction.Api.Common.RealTime;

public sealed record BidPlacedNotification(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    decimal PreviousPrice,
    Guid? OutbidBidderId,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);

public sealed record AuctionStatusChangedNotification(
    Guid AuctionId,
    string Status,
    decimal CurrentPrice,
    Guid? LeadingBidderId,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
