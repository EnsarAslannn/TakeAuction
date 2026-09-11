using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Watchlist.GetWatchlist;

public sealed class GetWatchlistEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/watchlist", async (
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var items = await sender.Send(new GetWatchlistQuery(principal.GetUserId()), cancellationToken);

                return Results.Ok(items);
            })
            .RequireAuthorization()
            .WithName("GetWatchlist")
            .WithTags("Watchlist")
            .WithSummary("Returns the lots the caller is watching: open ones first, soonest to close at the top.")
            .Produces<IReadOnlyList<WatchlistItem>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
