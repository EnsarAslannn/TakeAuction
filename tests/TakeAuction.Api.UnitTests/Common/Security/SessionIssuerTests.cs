using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.UnitTests.Common.Security;

public sealed class SessionIssuerTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "TakeAuction",
        Audience = "TakeAuction.Client",
        SigningKey = "unit-test-signing-key-at-least-32-characters",
        AccessTokenLifetimeMinutes = 15,
        RefreshTokenLifetimeDays = 7
    };

    private readonly AppDbContext _dbContext = TestHarness.CreateDbContext();
    private readonly FixedTimeProvider _time = new(TestHarness.Now);
    private readonly RefreshTokenGenerator _refreshTokens = new();

    [Fact]
    public async Task Starting_a_session_stores_only_the_hash_of_the_token_it_hands_out()
    {
        var user = await AddUserAsync();
        var issuer = CreateIssuer();

        var session = await issuer.StartAsync(user, CancellationToken.None);

        var stored = await _dbContext.RefreshTokens.SingleAsync();

        Assert.NotEqual(session.RefreshToken.Value, stored.TokenHash);
        Assert.Equal(_refreshTokens.Hash(session.RefreshToken.Value), stored.TokenHash);
        Assert.Equal(user.Id, stored.UserId);
        Assert.True(stored.IsActive(TestHarness.Now));
    }

    [Fact]
    public async Task A_session_expires_on_the_configured_horizon()
    {
        var user = await AddUserAsync();

        var session = await CreateIssuer().StartAsync(user, CancellationToken.None);

        Assert.Equal(TestHarness.Now.AddDays(Jwt.RefreshTokenLifetimeDays), session.RefreshToken.ExpiresAtUtc);
        Assert.Equal(TestHarness.Now.AddMinutes(Jwt.AccessTokenLifetimeMinutes), session.AccessToken.ExpiresAtUtc);
    }

    [Fact]
    public async Task Two_logins_open_two_independent_families()
    {
        var user = await AddUserAsync();
        var issuer = CreateIssuer();

        await issuer.StartAsync(user, CancellationToken.None);
        await issuer.StartAsync(user, CancellationToken.None);

        var families = await _dbContext.RefreshTokens.Select(token => token.FamilyId).ToListAsync();

        Assert.Equal(2, families.Distinct().Count());
    }

    /// <summary>
    /// Rotation is the whole point of the design: the presented token dies in the same save
    /// that mints its successor, so a chain never has two live links.
    /// </summary>
    [Fact]
    public async Task Rotating_retires_the_presented_token_and_keeps_the_family()
    {
        var user = await AddUserAsync();
        var issuer = CreateIssuer();

        var first = await issuer.StartAsync(user, CancellationToken.None);
        var current = await _dbContext.RefreshTokens.SingleAsync();

        _time.Advance(TimeSpan.FromMinutes(20));

        var second = await issuer.RotateAsync(user, current, CancellationToken.None);

        var tokens = await _dbContext.RefreshTokens.OrderBy(token => token.CreatedAtUtc).ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.Single(tokens, token => token.IsActive(_time.GetUtcNow()));

        var retired = tokens[0];
        var live = tokens[1];

        Assert.True(retired.IsRevoked);
        Assert.Equal(live.Id, retired.ReplacedByTokenId);
        Assert.Equal(retired.FamilyId, live.FamilyId);
        Assert.NotEqual(first.RefreshToken.Value, second.RefreshToken.Value);
    }

    [Fact]
    public async Task Rotating_extends_the_horizon_from_the_moment_of_the_refresh()
    {
        var user = await AddUserAsync();
        var issuer = CreateIssuer();

        await issuer.StartAsync(user, CancellationToken.None);
        var current = await _dbContext.RefreshTokens.SingleAsync();

        _time.Advance(TimeSpan.FromDays(3));

        var rotated = await issuer.RotateAsync(user, current, CancellationToken.None);

        Assert.Equal(_time.GetUtcNow().AddDays(Jwt.RefreshTokenLifetimeDays), rotated.RefreshToken.ExpiresAtUtc);
    }

    private SessionIssuer CreateIssuer() =>
        new(
            _dbContext,
            new JwtTokenGenerator(Options.Create(Jwt), _time),
            _refreshTokens,
            Options.Create(Jwt),
            _time);

    private async Task<User> AddUserAsync()
    {
        var user = User.Create("bidder@takeauction.test", "Demo Bidder", "not-a-real-hash", UserRole.Bidder);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return user;
    }
}
