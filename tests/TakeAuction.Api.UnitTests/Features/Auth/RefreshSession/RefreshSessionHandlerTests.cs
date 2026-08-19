using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auth.RefreshSession;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auth.RefreshSession;

public sealed class RefreshSessionHandlerTests
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_request_without_a_token_is_refused_before_any_lookup(string? presented)
    {
        var result = await CreateHandler().Handle(new RefreshSessionCommand(presented), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(RefreshRejection.MissingToken, result.Rejection);
        Assert.Null(result.Session);
    }

    [Fact]
    public async Task A_token_the_server_never_issued_is_refused()
    {
        await AddUserWithSessionAsync();

        var result = await CreateHandler()
            .Handle(new RefreshSessionCommand("a-token-that-was-never-issued"), CancellationToken.None);

        Assert.Equal(RefreshRejection.UnknownToken, result.Rejection);
    }

    [Fact]
    public async Task An_expired_token_is_refused()
    {
        var (_, token) = await AddUserWithSessionAsync();

        _time.Advance(TimeSpan.FromDays(Jwt.RefreshTokenLifetimeDays + 1));

        var result = await CreateHandler().Handle(new RefreshSessionCommand(token), CancellationToken.None);

        Assert.Equal(RefreshRejection.ExpiredToken, result.Rejection);
    }

    [Fact]
    public async Task A_live_token_buys_a_new_pair_and_retires_itself()
    {
        var (user, token) = await AddUserWithSessionAsync();

        _time.Advance(TimeSpan.FromMinutes(20));

        var result = await CreateHandler().Handle(new RefreshSessionCommand(token), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Session);
        Assert.NotEqual(token, result.Session.RefreshToken.Value);

        Assert.NotNull(result.User);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Equal(user.Email, result.User.Email);
        Assert.Equal(nameof(UserRole.Bidder), result.User.Role);
        Assert.Equal(result.Session.AccessToken.ExpiresAtUtc, result.User.ExpiresAtUtc);

        var stored = await _dbContext.RefreshTokens.OrderBy(entity => entity.CreatedAtUtc).ToListAsync();

        Assert.Equal(2, stored.Count);
        Assert.True(stored[0].IsRevoked);
        Assert.True(stored[1].IsActive(_time.GetUtcNow()));
    }

    [Fact]
    public async Task The_retired_token_stops_working_the_moment_it_is_rotated()
    {
        var (_, token) = await AddUserWithSessionAsync();
        var handler = CreateHandler();

        var first = await handler.Handle(new RefreshSessionCommand(token), CancellationToken.None);
        Assert.True(first.Succeeded);

        _dbContext.ChangeTracker.Clear();

        var replayed = await _dbContext.RefreshTokens
            .Where(entity => entity.TokenHash == _refreshTokens.Hash(token))
            .SingleAsync();

        Assert.True(replayed.IsRevoked);
        Assert.NotNull(replayed.ReplacedByTokenId);
    }

    private RefreshSessionHandler CreateHandler() =>
        new(
            _dbContext,
            _refreshTokens,
            new SessionIssuer(
                _dbContext,
                new JwtTokenGenerator(Options.Create(Jwt), _time),
                _refreshTokens,
                Options.Create(Jwt),
                _time),
            _time,
            NullLogger<RefreshSessionHandler>.Instance);

    private async Task<(User User, string RefreshToken)> AddUserWithSessionAsync()
    {
        var user = User.Create("bidder@takeauction.test", "Demo Bidder", "not-a-real-hash", UserRole.Bidder);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var issuer = new SessionIssuer(
            _dbContext,
            new JwtTokenGenerator(Options.Create(Jwt), _time),
            _refreshTokens,
            Options.Create(Jwt),
            _time);

        var session = await issuer.StartAsync(user, CancellationToken.None);

        _dbContext.ChangeTracker.Clear();

        return (user, session.RefreshToken.Value);
    }
}
