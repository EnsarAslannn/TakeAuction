using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.ExpireAuctions;

public sealed record AuctionEndedEvent(
    Guid AuctionId,
    Guid SellerId,
    Guid? WinningBidderId,
    decimal FinalPrice,
    int BidCount,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
