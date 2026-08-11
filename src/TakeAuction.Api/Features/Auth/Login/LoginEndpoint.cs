using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Login;

public sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/login", async (
                LoginRequest request,
                HttpContext httpContext,
                ISender sender,
                AuthCookieWriter cookieWriter,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);

                if (!result.Succeeded)
                {
                    return result.Rejection switch
                    {
                        LoginRejection.AccountDisabled => Results.Problem(
                            title: "Account disabled",
                            detail: "This account has been deactivated.",
                            statusCode: StatusCodes.Status403Forbidden),

                        _ => Results.Problem(
                            title: "Invalid credentials",
                            detail: "The email or password is incorrect.",
                            statusCode: StatusCodes.Status401Unauthorized)
                    };
                }

                cookieWriter.Write(httpContext, result.AccessToken!);

                return Results.Ok(result.User);
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .WithName("Login")
            .WithTags("Auth")
            .WithSummary("Signs a user in and issues the HttpOnly access token plus the CSRF double-submit cookie.")
            .Produces<AuthenticatedUserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
