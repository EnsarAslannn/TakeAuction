namespace TakeAuction.Api.Domain.Users;

public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Issue(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTimeOffset nowUtc,
        DateTimeOffset expiresAtUtc,
        Guid? tokenId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (familyId == Guid.Empty)
        {
            throw new ArgumentException("Family id is required.", nameof(familyId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (expiresAtUtc <= nowUtc)
        {
            throw new ArgumentException("Refresh token must expire in the future.", nameof(expiresAtUtc));
        }

        if (tokenId == Guid.Empty)
        {
            throw new ArgumentException("Token id must not be empty.", nameof(tokenId));
        }

        return new RefreshToken
        {
            Id = tokenId ?? Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public bool IsActive(DateTimeOffset nowUtc) => !IsRevoked && !IsExpired(nowUtc);

    public bool WasRotatedWithin(DateTimeOffset nowUtc, TimeSpan grace) =>
        ReplacedByTokenId is not null
        && RevokedAtUtc is { } revokedAt
        && (nowUtc - revokedAt).Duration() <= grace;

    public void Revoke(DateTimeOffset nowUtc)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAtUtc = nowUtc;
    }
}
