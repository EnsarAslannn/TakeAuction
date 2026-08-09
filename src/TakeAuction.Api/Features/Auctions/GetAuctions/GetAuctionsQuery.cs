using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.GetAuctions;

public sealed record GetAuctionsQuery(
    int Page = 1,
    int PageSize = 20,
    AuctionStatus? Status = null,
    Guid? SellerId = null,
    string? Search = null) : IRequest<PagedResult<AuctionListItem>>
{
    public const int MaxPageSize = 100;

    public GetAuctionsQuery Normalize() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        },
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()
    };
}

public sealed record AuctionListItem(
    Guid Id,
    string Title,
    decimal StartingPrice,
    decimal CurrentPrice,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    Guid SellerId);
