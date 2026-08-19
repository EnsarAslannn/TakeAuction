using MediatR;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.RefreshSession;

public sealed record RefreshSessionCommand(string? RefreshToken) : IRequest<RefreshSessionResult>;

public enum RefreshRejection
{
    None = 0,
    MissingToken = 1,
    UnknownToken = 2,
    ExpiredToken = 3,

    ReusedToken = 4,
    AccountUnavailable = 5
}

public sealed record RefreshSessionResult(
    RefreshRejection Rejection,
    AuthenticatedUserResponse? User,
    IssuedSession? Session)
{
    public bool Succeeded => Rejection == RefreshRejection.None;

    public static RefreshSessionResult Accepted(AuthenticatedUserResponse user, IssuedSession session) =>
        new(RefreshRejection.None, user, session);

    public static RefreshSessionResult Rejected(RefreshRejection rejection) =>
        new(rejection, null, null);
}
