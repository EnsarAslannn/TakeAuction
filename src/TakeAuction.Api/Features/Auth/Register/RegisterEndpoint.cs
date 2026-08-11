using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Auth.Register;

public sealed class RegisterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/register", async (
                RegisterRequest request,
                HttpContext httpContext,
                ISender sender,
                AuthCookieWriter cookieWriter,
                CancellationToken cancellationToken) =>
            {
                var command = new RegisterCommand(
                    request.Email,
                    request.DisplayName,
                    request.Password,
                    string.IsNullOrWhiteSpace(request.Role) ? nameof(UserRole.Bidder) : request.Role);

                var result = await sender.Send(command, cancellationToken);

                if (result.EmailAlreadyInUse)
                {
                    return Results.Problem(
                        title: "Email already in use",
                        detail: "An account already exists for this email address.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                cookieWriter.Write(httpContext, result.AccessToken!);

                return Results.Created($"/api/v1/auth/me", result.User);
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .WithName("Register")
            .WithTags("Auth")
            .WithSummary("Registers a bidder or seller and issues the session cookie pair.")
            .Produces<AuthenticatedUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
