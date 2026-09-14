using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auctions.GetBidStanding;

public sealed class GetBidStandingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auctions/{id:guid}/standing", async (
                Guid id,
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var standing = await sender.Send(
                    new GetBidStandingQuery(id, principal.GetUserId()),
                    cancellationToken);

                return standing is null
                    ? Results.Problem(
                        title: "Auction not found",
                        detail: $"No auction exists with id '{id}'.",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(standing);
            })
            .RequireAuthorization()
            .WithName("GetBidStanding")
            .WithTags("Auctions")
            .WithSummary("Returns where the caller stands on an auction: whether they lead, their own ceiling and the least they may bid next.")
            .Produces<BidStandingResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
