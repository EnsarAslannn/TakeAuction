using Microsoft.AspNetCore.Identity;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password) => _inner.HashPassword(null!, password);

    public PasswordVerificationOutcome Verify(string passwordHash, string providedPassword)
    {
        return _inner.VerifyHashedPassword(null!, passwordHash, providedPassword) switch
        {
            PasswordVerificationResult.Success => PasswordVerificationOutcome.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerificationOutcome.SuccessRehashNeeded,
            _ => PasswordVerificationOutcome.Failed
        };
    }
}
