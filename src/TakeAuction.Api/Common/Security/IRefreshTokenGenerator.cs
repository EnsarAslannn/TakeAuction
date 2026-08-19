namespace TakeAuction.Api.Common.Security;

public interface IRefreshTokenGenerator
{
    RefreshTokenValue Generate();

    string Hash(string value);
}

public sealed record RefreshTokenValue(string Value, string Hash);
