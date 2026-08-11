using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auth.GetCurrentUser;

public sealed class GetCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auth/me", async (
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var user = await sender.Send(new GetCurrentUserQuery(principal.GetUserId()), cancellationToken);

                return user is null
                    ? Results.Problem(
                        title: "Account not found",
                        detail: "The authenticated principal no longer maps to an active account.",
                        statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(user);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .WithSummary("Returns the profile behind the current session cookie.")
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
