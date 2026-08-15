using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.CancelAuction;

public sealed record AuctionCancelledEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal CurrentPrice,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
