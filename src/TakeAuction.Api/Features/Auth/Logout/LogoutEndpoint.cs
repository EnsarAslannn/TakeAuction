using MediatR;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Logout;

public sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/logout", async (
                HttpContext httpContext,
                ISender sender,
                AuthCookieWriter cookieWriter,
                CancellationToken cancellationToken) =>
            {
                var presented = AuthCookieWriter.ReadRefreshToken(httpContext);

                await sender.Send(new LogoutCommand(presented), cancellationToken);

                cookieWriter.Clear(httpContext);

                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithTags("Auth")
            .WithSummary("Revokes the session on the server and clears the auth cookies.")
            .Produces(StatusCodes.Status204NoContent);
    }
}
