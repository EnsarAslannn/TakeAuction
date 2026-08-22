using MediatR;

namespace TakeAuction.Api.Features.Auth.GetHubTicket;

public sealed record GetHubTicketQuery(Guid UserId) : IRequest<HubTicketResponse?>;

public sealed record HubTicketResponse(string Token, DateTimeOffset ExpiresAtUtc);
