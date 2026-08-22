using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Security;

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);

    AccessToken GenerateHubTicket(User user);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAtUtc);
