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
                var userId = principal.GetUserId();

                if (userId == Guid.Empty)
                {
                    return Results.NoContent();
                }

                var user = await sender.Send(new GetCurrentUserQuery(userId), cancellationToken);

                return user is null
                    ? Results.NoContent()
                    : Results.Ok(user);
            })
            .AllowAnonymous()
            .WithName("GetCurrentUser")
            .WithTags("Auth")
            .WithSummary("Returns the profile behind the current session cookie, or 204 when there is no session.")
            .Produces<CurrentUserResponse>()
            .Produces(StatusCodes.Status204NoContent);
    }
}
