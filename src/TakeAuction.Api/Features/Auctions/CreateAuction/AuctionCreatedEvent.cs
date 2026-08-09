using TakeAuction.Api.Common.Messaging;

namespace TakeAuction.Api.Features.Auctions.CreateAuction;

public sealed record AuctionCreatedEvent(
    Guid AuctionId,
    Guid SellerId,
    decimal StartingPrice,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;
