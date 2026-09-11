using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Notifications.GetNotifications;

public sealed class GetNotificationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/notifications", async (
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken,
                int take = GetNotificationsQuery.DefaultTake) =>
            {
                var response = await sender.Send(
                    new GetNotificationsQuery(principal.GetUserId(), take),
                    cancellationToken);

                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetNotifications")
            .WithTags("Notifications")
            .WithSummary("Returns the caller's most recent notifications, newest first, with the number still unread.")
            .Produces<NotificationsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
