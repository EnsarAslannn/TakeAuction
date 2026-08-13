using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Security;

public interface ISessionIssuer
{
    /// <summary>Opens a new session chain — a fresh login or registration.</summary>
    Task<IssuedSession> StartAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Extends an existing chain: the presented token is retired and points at its successor,
    /// both in a single save so a crash can never leave two live links.
    /// </summary>
    Task<IssuedSession> RotateAsync(User user, RefreshToken current, CancellationToken cancellationToken);
}

public sealed record IssuedSession(AccessToken AccessToken, IssuedRefreshToken RefreshToken);

public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAtUtc);

public sealed class SessionIssuer : ISessionIssuer
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenGenerator _accessTokens;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public SessionIssuer(
        AppDbContext dbContext,
        IJwtTokenGenerator accessTokens,
        IRefreshTokenGenerator refreshTokens,
        IOptions<JwtOptions> options,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _accessTokens = accessTokens;
        _refreshTokens = refreshTokens;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task<IssuedSession> StartAsync(User user, CancellationToken cancellationToken) =>
        IssueAsync(user, Guid.CreateVersion7(), rotating: null, cancellationToken);

    public Task<IssuedSession> RotateAsync(User user, RefreshToken current, CancellationToken cancellationToken) =>
        IssueAsync(user, current.FamilyId, current, cancellationToken);

    private async Task<IssuedSession> IssueAsync(
        User user,
        Guid familyId,
        RefreshToken? rotating,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(_options.RefreshTokenLifetimeDays);

        var value = _refreshTokens.Generate();
        var token = RefreshToken.Issue(user.Id, familyId, value.Hash, now, expiresAt);

        rotating?.ReplaceWith(token, now);

        await _dbContext.RefreshTokens.AddAsync(token, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedSession(
            _accessTokens.Generate(user),
            new IssuedRefreshToken(value.Value, expiresAt));
    }
}
