using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.RefreshSession;

public sealed class RefreshSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/refresh", async (
                HttpContext httpContext,
                ISender sender,
                AuthCookieWriter cookieWriter,
                CancellationToken cancellationToken) =>
            {
                var presented = AuthCookieWriter.ReadRefreshToken(httpContext);

                var result = await sender.Send(new RefreshSessionCommand(presented), cancellationToken);

                if (!result.Succeeded)
                {
                    if (result.EndsTheSession)
                    {
                        cookieWriter.Clear(httpContext);
                    }

                    return ToProblem(result.Rejection);
                }

                cookieWriter.Write(httpContext, result.Session!);

                return Results.Ok(result.User);
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .WithName("RefreshSession")
            .WithTags("Auth")
            .WithSummary("Rotates the refresh cookie and issues a new access token.")
            .Produces<AuthenticatedUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static IResult ToProblem(RefreshRejection rejection) => rejection switch
    {
        RefreshRejection.ConcurrentRotation => Results.Problem(
            title: "Session already refreshed",
            detail: "Another request rotated this session a moment ago. The session is intact; retry the call.",
            statusCode: StatusCodes.Status409Conflict),

        RefreshRejection.ReusedToken => Results.Problem(
            title: "Session ended",
            detail: "This session was ended because its refresh token was presented twice.",
            statusCode: StatusCodes.Status401Unauthorized),

        RefreshRejection.AccountUnavailable => Results.Problem(
            title: "Account unavailable",
            detail: "The account behind this session is no longer active.",
            statusCode: StatusCodes.Status401Unauthorized),

        _ => Results.Problem(
            title: "Session cannot be refreshed",
            detail: "The refresh token is missing, expired or not recognised.",
            statusCode: StatusCodes.Status401Unauthorized)
    };
}
