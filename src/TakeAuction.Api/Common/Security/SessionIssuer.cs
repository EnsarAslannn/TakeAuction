using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Common.Security;

public interface ISessionIssuer
{
    Task<IssuedSession> StartAsync(User user, CancellationToken cancellationToken);

    Task<IssuedSession> ContinueAsync(
        User user,
        Guid familyId,
        Guid tokenId,
        CancellationToken cancellationToken);
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
        ContinueAsync(user, Guid.CreateVersion7(), Guid.CreateVersion7(), cancellationToken);

    public async Task<IssuedSession> ContinueAsync(
        User user,
        Guid familyId,
        Guid tokenId,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddDays(_options.RefreshTokenLifetimeDays);

        var value = _refreshTokens.Generate();
        var token = RefreshToken.Issue(user.Id, familyId, value.Hash, now, expiresAt, tokenId);

        await _dbContext.RefreshTokens.AddAsync(token, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedSession(
            _accessTokens.Generate(user),
            new IssuedRefreshToken(value.Value, expiresAt));
    }
}
