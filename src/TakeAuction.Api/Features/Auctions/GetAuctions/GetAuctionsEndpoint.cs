using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.GetAuctions;

public sealed class GetAuctionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auctions", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20,
                AuctionStatus? status = null,
                Guid? sellerId = null,
                string? search = null) =>
            {
                var query = new GetAuctionsQuery(page, pageSize, status, sellerId, search);
                var result = await sender.Send(query, cancellationToken);

                return Results.Ok(result);
            })
            .AllowAnonymous()
            .WithName("GetAuctions")
            .WithTags("Auctions")
            .WithSummary("Returns a paged, filterable list of auctions served from the Redis cache when warm.")
            .Produces<PagedResult<AuctionListItem>>();
    }
}
