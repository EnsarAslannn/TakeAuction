using MediatR;

namespace TakeAuction.Api.Features.Auth.Logout;

public sealed record LogoutCommand(string? RefreshToken) : IRequest<LogoutResult>;

public sealed record LogoutResult(bool SessionRevoked);
