using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auctions.GetAuctionById;

public sealed class GetAuctionByIdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auctions/{id:guid}", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var auction = await sender.Send(new GetAuctionByIdQuery(id), cancellationToken);

                return auction is null
                    ? Results.Problem(
                        title: "Auction not found",
                        detail: $"No auction exists with id '{id}'.",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(auction);
            })
            .AllowAnonymous()
            .WithName("GetAuctionById")
            .WithTags("Auctions")
            .WithSummary("Returns a single auction, served from the Redis cache when warm.")
            .Produces<AuctionDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
