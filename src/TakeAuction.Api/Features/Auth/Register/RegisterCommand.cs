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
    Common.Security.AccessToken? AccessToken)
{
    public static RegisterResult Conflict() => new(true, null, null);

    public static RegisterResult Created(
        AuthenticatedUserResponse user,
        Common.Security.AccessToken accessToken) => new(false, user, accessToken);
}
