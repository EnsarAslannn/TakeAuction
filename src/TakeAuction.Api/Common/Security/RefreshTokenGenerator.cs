using System.Security.Cryptography;
using System.Text;

namespace TakeAuction.Api.Common.Security;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public const int TokenLengthInCharacters = 64;

    public RefreshTokenValue Generate()
    {
        var value = RandomNumberGenerator.GetHexString(TokenLengthInCharacters, lowercase: true);

        return new RefreshTokenValue(value, Hash(value));
    }

    public string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
