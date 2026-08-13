using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auctions.GetAuctionBids;

public sealed class GetAuctionBidsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auctions/{id:guid}/bids", async (
                Guid id,
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var result = await sender.Send(new GetAuctionBidsQuery(id, page, pageSize), cancellationToken);

                return result is null
                    ? Results.Problem(
                        title: "Auction not found",
                        detail: $"No auction exists with id '{id}'.",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(result);
            })
            .AllowAnonymous()
            .WithName("GetAuctionBids")
            .WithTags("Auctions")
            .WithSummary("Returns the bidding history of an auction, highest first.")
            .Produces<PagedResult<AuctionBidItem>>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
