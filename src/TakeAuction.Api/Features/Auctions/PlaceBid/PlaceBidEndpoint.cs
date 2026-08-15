using System.Globalization;
using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

public sealed class PlaceBidEndpoint : IEndpoint
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public const string IdempotentReplayHeader = "Idempotent-Replay";

    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auctions/{id:guid}/bids", async (
                Guid id,
                PlaceBidRequest request,
                ClaimsPrincipal principal,
                HttpContext httpContext,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new PlaceBidCommand(
                    id,
                    principal.GetUserId(),
                    request.Amount,
                    ReadIdempotencyKey(httpContext));

                var result = await sender.Send(command, cancellationToken);

                if (!result.Succeeded)
                {
                    return ToProblem(id, result);
                }

                if (result.Replayed)
                {
                    httpContext.Response.Headers[IdempotentReplayHeader] = "true";
                }

                return Results.Ok(result.Response);
            })
            .RequireAuthorization()
            .WithName("PlaceBid")
            .WithTags("Auctions")
            .WithSummary("Places a bid on an auction under PostgreSQL optimistic concurrency control.")
            .Produces<PlaceBidResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static string? ReadIdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers.TryGetValue(IdempotencyHeader, out var values)
            ? values.ToString()
            : null;

    private static IResult ToProblem(Guid auctionId, PlaceBidResult result) => result.Rejection switch
    {
        BidRejection.AuctionNotFound => Results.Problem(
            title: "Auction not found",
            detail: $"No auction exists with id '{auctionId}'.",
            statusCode: StatusCodes.Status404NotFound),

        BidRejection.SellerCannotBid => Results.Problem(
            title: "Sellers cannot bid on their own auction",
            detail: "The authenticated user owns this auction.",
            statusCode: StatusCodes.Status403Forbidden),

        BidRejection.AuctionNotOpen => Results.Problem(
            title: "Auction is not open for bidding",
            detail: "The auction has not started yet, or it has already ended or been cancelled.",
            statusCode: StatusCodes.Status409Conflict),

        BidRejection.BidTooLow => Results.Problem(
            title: "Bid is too low",
            detail: string.Create(
                CultureInfo.InvariantCulture,
                $"The bid must be at least {result.MinimumAcceptableBid:0.00}."),
            statusCode: StatusCodes.Status400BadRequest),

        BidRejection.ConcurrencyConflict => Results.Problem(
            title: "Bid could not be applied",
            detail: "The auction was updated by other bidders too many times. Please retry.",
            statusCode: StatusCodes.Status409Conflict),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}
