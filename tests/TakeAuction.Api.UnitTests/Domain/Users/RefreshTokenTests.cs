using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Users;

public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid FamilyId = Guid.CreateVersion7();

    [Fact]
    public void An_issued_token_is_active_until_it_expires()
    {
        var token = Issue();

        Assert.True(token.IsActive(TestHarness.Now));
        Assert.False(token.IsRevoked);
        Assert.False(token.IsExpired(TestHarness.Now));
        Assert.Equal(FamilyId, token.FamilyId);
        Assert.Null(token.ReplacedByTokenId);
    }

    [Fact]
    public void A_token_is_expired_from_the_instant_it_lapses()
    {
        var expiresAt = TestHarness.Now.AddDays(7);
        var token = Issue(expiresAt);

        Assert.False(token.IsExpired(expiresAt.AddTicks(-1)));
        Assert.True(token.IsExpired(expiresAt));
        Assert.False(token.IsActive(expiresAt));
    }

    [Fact]
    public void Revoking_records_when_it_happened_and_is_idempotent()
    {
        var token = Issue();
        var firstRevoke = TestHarness.Now.AddMinutes(5);

        token.Revoke(firstRevoke);
        token.Revoke(firstRevoke.AddMinutes(5));

        Assert.True(token.IsRevoked);
        Assert.Equal(firstRevoke, token.RevokedAtUtc);
        Assert.False(token.IsActive(TestHarness.Now));
    }

    [Fact]
    public void Replacing_retires_the_old_link_and_points_it_at_the_new_one()
    {
        var current = Issue();
        var replacement = Issue();
        var rotatedAt = TestHarness.Now.AddMinutes(10);

        current.ReplaceWith(replacement, rotatedAt);

        Assert.True(current.IsRevoked);
        Assert.Equal(rotatedAt, current.RevokedAtUtc);
        Assert.Equal(replacement.Id, current.ReplacedByTokenId);
        Assert.True(replacement.IsActive(rotatedAt));
    }

    [Fact]
    public void A_token_cannot_be_replaced_by_one_from_another_family()
    {
        var current = Issue();
        var stranger = RefreshToken.Issue(
            UserId,
            Guid.CreateVersion7(),
            "another-family-hash",
            TestHarness.Now,
            TestHarness.Now.AddDays(7));

        Assert.Throws<ArgumentException>(() => current.ReplaceWith(stranger, TestHarness.Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_token_without_a_hash_is_rejected(string hash)
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(UserId, FamilyId, hash, TestHarness.Now, TestHarness.Now.AddDays(7)));
    }

    [Fact]
    public void A_token_that_expires_in_the_past_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(UserId, FamilyId, "hash", TestHarness.Now, TestHarness.Now.AddSeconds(-1)));
    }

    [Fact]
    public void A_token_without_an_owner_or_a_family_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(Guid.Empty, FamilyId, "hash", TestHarness.Now, TestHarness.Now.AddDays(7)));

        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(UserId, Guid.Empty, "hash", TestHarness.Now, TestHarness.Now.AddDays(7)));
    }

    private static RefreshToken Issue(DateTimeOffset? expiresAt = null) =>
        RefreshToken.Issue(
            UserId,
            FamilyId,
            $"hash-{Guid.CreateVersion7():N}",
            TestHarness.Now,
            expiresAt ?? TestHarness.Now.AddDays(7));
}
