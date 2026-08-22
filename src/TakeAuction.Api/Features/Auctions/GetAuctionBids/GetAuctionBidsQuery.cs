using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auctions.GetAuctionBids;

public sealed record GetAuctionBidsQuery(Guid AuctionId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AuctionBidItem>?>
{
    public const int MaxPageSize = 100;

    public const int MaxPage = 10_000;

    public GetAuctionBidsQuery Normalize() => this with
    {
        Page = Page switch
        {
            < 1 => 1,
            > MaxPage => MaxPage,
            _ => Page
        },
        PageSize = PageSize switch
        {
            < 1 => 20,
            > MaxPageSize => MaxPageSize,
            _ => PageSize
        }
    };
}

public sealed record AuctionBidItem(
    Guid Id,
    decimal Amount,
    bool IsAutomatic,
    DateTimeOffset PlacedAtUtc,
    Guid BidderId);
