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

    /// <summary>
    /// Unsalted SHA-256 on purpose: the lookup has to find a row from the presented token, and
    /// 256 bits of entropy leaves nothing for a dictionary attack to chew on.
    /// </summary>
    public string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
