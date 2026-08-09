using MediatR;

namespace TakeAuction.Api.Features.Auctions.CreateAuction;

public sealed record CreateAuctionCommand(
    Guid SellerId,
    string Title,
    string Description,
    decimal StartingPrice,
    decimal MinimumBidIncrement,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc) : IRequest<CreateAuctionResponse>;

public sealed record CreateAuctionRequest(
    string Title,
    string Description,
    decimal StartingPrice,
    decimal MinimumBidIncrement,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc);

public sealed record CreateAuctionResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset CreatedAtUtc);
