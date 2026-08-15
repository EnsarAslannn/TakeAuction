namespace TakeAuction.Api.Common.RealTime;

public sealed record BidPlacedNotification(
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

/// <summary>
/// Sent to one bidder rather than to a group: it is the only message in the system that is
/// about a person instead of a lot. It carries the title because the whole point is reaching
/// somebody who has moved on and is looking at something else entirely.
/// </summary>
public sealed record OutbidNotification(
    Guid AuctionId,
    string AuctionTitle,
    decimal CurrentPrice,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);

public sealed record AuctionStatusChangedNotification(
    Guid AuctionId,
    string Status,
    decimal CurrentPrice,
    Guid? LeadingBidderId,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc);
