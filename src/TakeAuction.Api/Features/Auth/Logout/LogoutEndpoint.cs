using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Logout;

public sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/auth/logout", (HttpContext httpContext, AuthCookieWriter cookieWriter) =>
            {
                cookieWriter.Clear(httpContext);

                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("Logout")
            .WithTags("Auth")
            .WithSummary("Clears the access token and CSRF cookies.")
            .Produces(StatusCodes.Status204NoContent);
    }
}
