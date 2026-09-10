using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.OpenAuctions;

public sealed record AuctionOpenedEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal CurrentPrice,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
