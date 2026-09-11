using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Notifications.MarkNotificationsRead;

public sealed class MarkNotificationsReadEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapPost("/notifications/read", async (
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new MarkNotificationsReadCommand(principal.GetUserId()), cancellationToken);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("MarkNotificationsRead")
            .WithTags("Notifications")
            .WithSummary("Marks every unread notification of the caller as read.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
