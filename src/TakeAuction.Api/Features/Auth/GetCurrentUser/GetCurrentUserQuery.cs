using MediatR;

namespace TakeAuction.Api.Features.Auth.GetCurrentUser;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<CurrentUserResponse?>;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);
