using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Watchlist.UnwatchAuction;

public sealed class UnwatchAuctionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapDelete("/auctions/{id:guid}/watch", async (
                Guid id,
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new UnwatchAuctionCommand(principal.GetUserId(), id), cancellationToken);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("UnwatchAuction")
            .WithTags("Watchlist")
            .WithSummary("Removes a lot from the caller's watchlist. Removing a lot that is not on it changes nothing.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
