using MediatR;

namespace TakeAuction.Api.Features.Watchlist.UnwatchAuction;

public sealed record UnwatchAuctionCommand(Guid UserId, Guid AuctionId) : IRequest<bool>;
