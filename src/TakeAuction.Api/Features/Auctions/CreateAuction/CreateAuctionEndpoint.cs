using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Auctions.CreateAuction;

public sealed class CreateAuctionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auctions", async (
                CreateAuctionRequest request,
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CreateAuctionCommand(
                    principal.GetUserId(),
                    request.Title,
                    request.Description,
                    request.StartingPrice,
                    request.MinimumBidIncrement,
                    request.StartsAtUtc,
                    request.EndsAtUtc,
                    request.ImageUrl);

                var response = await sender.Send(command, cancellationToken);

                return Results.CreatedAtRoute("GetAuctionById", new { id = response.Id }, response);
            })
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Seller), nameof(UserRole.Admin)))
            .WithName("CreateAuction")
            .WithTags("Auctions")
            .WithSummary("Creates a new auction owned by the authenticated seller.")
            .Produces<CreateAuctionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
