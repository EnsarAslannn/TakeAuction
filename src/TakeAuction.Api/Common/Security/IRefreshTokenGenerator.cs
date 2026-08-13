namespace TakeAuction.Api.Common.Security;

public interface IRefreshTokenGenerator
{
    RefreshTokenValue Generate();

    string Hash(string value);
}

/// <summary>
/// The clear-text half goes to the browser; only the hash is ever persisted, so a database
/// leak hands an attacker nothing they can present at the refresh endpoint.
/// </summary>
public sealed record RefreshTokenValue(string Value, string Hash);
