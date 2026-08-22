using System.Security.Claims;
using MediatR;
using TakeAuction.Api.Common.Api;

namespace TakeAuction.Api.Features.Auth.GetHubTicket;

public sealed class GetHubTicketEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder builder)
    {
        builder.MapGet("/auth/hub-ticket", async (
                ClaimsPrincipal principal,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var ticket = await sender.Send(new GetHubTicketQuery(principal.GetUserId()), cancellationToken);

                return ticket is null
                    ? Results.Problem(
                        title: "Session unavailable",
                        detail: "The account behind this session is no longer active.",
                        statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(ticket);
            })
            .RequireAuthorization()
            .WithName("GetHubTicket")
            .WithTags("Auth")
            .WithSummary("Issues a short-lived ticket that authenticates a SignalR connection made from another site.")
            .Produces<HubTicketResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
