using MediatR;

namespace TakeAuction.Api.Features.Auctions.GetAuctionById;

public sealed record GetAuctionByIdQuery(Guid AuctionId) : IRequest<AuctionDetailResponse?>;

public sealed record AuctionDetailResponse(
    Guid Id,
    string Title,
    string Description,
    decimal StartingPrice,
    decimal CurrentPrice,
    decimal MinimumBidIncrement,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    DateTimeOffset CreatedAtUtc,
    Guid SellerId,
    string SellerDisplayName);
