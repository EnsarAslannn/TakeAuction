using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Auctions.CancelAuction;

public sealed class CancelAuctionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auctions/{id:guid}/cancel", async (
                Guid id,
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new CancelAuctionCommand(id, principal.GetUserId());

                var result = await sender.Send(command, cancellationToken);

                return result.Succeeded ? Results.Ok(result.Response) : ToProblem(id, result);
            })
            .RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Seller), nameof(UserRole.Admin)))
            .WithName("CancelAuction")
            .WithTags("Auctions")
            .WithSummary("Withdraws a lot that has not been bid on, on behalf of the seller who listed it.")
            .Produces<CancelAuctionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static IResult ToProblem(Guid auctionId, CancelAuctionResult result) => result.Rejection switch
    {
        CancelRejection.AuctionNotFound => Results.Problem(
            title: "Auction not found",
            detail: $"No auction exists with id '{auctionId}'.",
            statusCode: StatusCodes.Status404NotFound),

        // Deliberately the same answer a stranger gets for a lot that does not exist: telling
        // them it exists but belongs to somebody else is more than they are entitled to know.
        CancelRejection.NotTheSeller => Results.Problem(
            title: "Auction not found",
            detail: $"No auction exists with id '{auctionId}'.",
            statusCode: StatusCodes.Status404NotFound),

        CancelRejection.AlreadyBidOn => Results.Problem(
            title: "The lot has already been bid on",
            detail: "Bidders have committed to this lot, so it can no longer be withdrawn. It runs to its close.",
            statusCode: StatusCodes.Status409Conflict),

        CancelRejection.AlreadyClosed => Results.Problem(
            title: "The lot has already closed",
            detail: "This auction has reached its end time and can no longer be withdrawn.",
            statusCode: StatusCodes.Status409Conflict),

        CancelRejection.AlreadyCancelled => Results.Problem(
            title: "The lot has already been withdrawn",
            detail: "This auction was withdrawn earlier.",
            statusCode: StatusCodes.Status409Conflict),

        CancelRejection.ConcurrencyConflict => Results.Problem(
            title: "The lot could not be withdrawn",
            detail: "The auction was updated by other bidders too many times. Please retry.",
            statusCode: StatusCodes.Status409Conflict),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
