using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Watchlist.WatchAuction;

public sealed class WatchAuctionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPut("/auctions/{id:guid}/watch", async (
                Guid id,
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new WatchAuctionCommand(principal.GetUserId(), id), cancellationToken);

                return result switch
                {
                    WatchAuctionResult.Watching => Results.NoContent(),

                    WatchAuctionResult.AuctionNotFound => Results.Problem(
                        title: "Auction not found",
                        detail: $"No auction exists with id '{id}'.",
                        statusCode: StatusCodes.Status404NotFound),

                    WatchAuctionResult.AuctionClosed => Results.Problem(
                        title: "The lot has already closed",
                        detail: "A lot that has ended or been withdrawn cannot be added to a watchlist.",
                        statusCode: StatusCodes.Status409Conflict),

                    _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
                };
            })
            .RequireAuthorization()
            .WithName("WatchAuction")
            .WithTags("Watchlist")
            .WithSummary("Adds a lot to the caller's watchlist. Watching a lot already on the list changes nothing.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
