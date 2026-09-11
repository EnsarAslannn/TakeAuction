using MediatR;

namespace TakeAuction.Api.Features.Watchlist.GetWatchlist;

public sealed record GetWatchlistQuery(Guid UserId) : IRequest<IReadOnlyList<WatchlistItem>>
{
    public const int MaxItems = 200;
}

public sealed record WatchlistItem(
    Guid Id,
    string Title,
    string? ImageUrl,
    decimal StartingPrice,
    decimal CurrentPrice,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    Guid SellerId,
    DateTimeOffset WatchedAtUtc);
