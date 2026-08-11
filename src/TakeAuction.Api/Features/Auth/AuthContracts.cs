namespace TakeAuction.Api.Features.Auth;

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset ExpiresAtUtc);
