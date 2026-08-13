using MediatR;

namespace TakeAuction.Api.Features.Auth.Register;

public sealed record RegisterCommand(
    string Email,
    string DisplayName,
    string Password,
    string Role) : IRequest<RegisterResult>;

public sealed record RegisterRequest(
    string Email,
    string DisplayName,
    string Password,
    string? Role);

public sealed record RegisterResult(
    bool EmailAlreadyInUse,
    AuthenticatedUserResponse? User,
    Common.Security.IssuedSession? Session)
{
    public static RegisterResult Conflict() => new(true, null, null);

    public static RegisterResult Created(
        AuthenticatedUserResponse user,
        Common.Security.IssuedSession session) => new(false, user, session);
}
