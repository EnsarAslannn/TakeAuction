using MediatR;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResult(
    LoginRejection Rejection,
    AuthenticatedUserResponse? User,
    IssuedSession? Session)
{
    public bool Succeeded => Rejection == LoginRejection.None;

    public static LoginResult Accepted(AuthenticatedUserResponse user, IssuedSession session) =>
        new(LoginRejection.None, user, session);

    public static LoginResult Rejected(LoginRejection rejection) => new(rejection, null, null);
}

public enum LoginRejection
{
    None = 0,
    InvalidCredentials = 1,
    AccountDisabled = 2
}
