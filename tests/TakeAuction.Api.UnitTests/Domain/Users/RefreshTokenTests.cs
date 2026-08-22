using Microsoft.EntityFrameworkCore;
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
    public async Task A_token_rotated_moments_ago_reads_as_a_concurrent_refresh()
    {
        var rotatedAt = TestHarness.Now.AddMinutes(10);
        var token = await RotatedAsync(rotatedAt);

        Assert.True(token.IsRevoked);
        Assert.Equal(rotatedAt, token.RevokedAtUtc);
        Assert.NotNull(token.ReplacedByTokenId);

        Assert.True(token.WasRotatedWithin(rotatedAt, TimeSpan.FromSeconds(30)));
        Assert.True(token.WasRotatedWithin(rotatedAt.AddSeconds(30), TimeSpan.FromSeconds(30)));
        Assert.False(token.WasRotatedWithin(rotatedAt.AddSeconds(31), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task A_rotation_stamped_just_after_the_reader_read_the_clock_is_still_a_race()
    {
        var rotatedAt = TestHarness.Now.AddMinutes(10);
        var token = await RotatedAsync(rotatedAt);

        var readTheClockFirst = rotatedAt.AddMilliseconds(-40);

        Assert.True(token.WasRotatedWithin(readTheClockFirst, TimeSpan.FromSeconds(30)));
        Assert.False(token.WasRotatedWithin(rotatedAt.AddSeconds(-31), TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void A_token_revoked_without_a_replacement_is_never_a_concurrent_refresh()
    {
        var token = Issue();
        var revokedAt = TestHarness.Now.AddMinutes(10);

        token.Revoke(revokedAt);

        Assert.False(token.WasRotatedWithin(revokedAt, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void A_live_token_is_never_a_concurrent_refresh()
    {
        Assert.False(Issue().WasRotatedWithin(TestHarness.Now, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void An_explicit_token_id_is_honoured_and_may_not_be_empty()
    {
        var id = Guid.CreateVersion7();

        Assert.Equal(
            id,
            RefreshToken.Issue(UserId, FamilyId, "hash", TestHarness.Now, TestHarness.Now.AddDays(7), id).Id);

        Assert.Throws<ArgumentException>(() =>
            RefreshToken.Issue(UserId, FamilyId, "hash", TestHarness.Now, TestHarness.Now.AddDays(7), Guid.Empty));
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

    private static async Task<RefreshToken> RotatedAsync(DateTimeOffset rotatedAt)
    {
        using var dbContext = TestHarness.CreateDbContext();

        var token = Issue();
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        var entry = dbContext.Entry(token);
        entry.Property(candidate => candidate.RevokedAtUtc).CurrentValue = rotatedAt;
        entry.Property(candidate => candidate.ReplacedByTokenId).CurrentValue = Guid.CreateVersion7();
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        return await dbContext.RefreshTokens.SingleAsync(candidate => candidate.Id == token.Id);
    }

    private static RefreshToken Issue(DateTimeOffset? expiresAt = null) =>
        RefreshToken.Issue(
            UserId,
            FamilyId,
            $"hash-{Guid.CreateVersion7():N}",
            TestHarness.Now,
            expiresAt ?? TestHarness.Now.AddDays(7));
}
