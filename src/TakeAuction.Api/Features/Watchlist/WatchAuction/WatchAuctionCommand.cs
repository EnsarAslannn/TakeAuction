using MediatR;

namespace TakeAuction.Api.Features.Watchlist.WatchAuction;

public sealed record WatchAuctionCommand(Guid UserId, Guid AuctionId) : IRequest<WatchAuctionResult>;

public enum WatchAuctionResult
{
    Watching = 0,
    AuctionNotFound = 1,
    AuctionClosed = 2
}
