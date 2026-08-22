using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.RefreshSession;

public sealed class RefreshSessionHandler : IRequestHandler<RefreshSessionCommand, RefreshSessionResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly ISessionIssuer _sessionIssuer;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _rotationGrace;
    private readonly ILogger<RefreshSessionHandler> _logger;

    public RefreshSessionHandler(
        AppDbContext dbContext,
        IRefreshTokenGenerator refreshTokens,
        ISessionIssuer sessionIssuer,
        IOptions<JwtOptions> options,
        TimeProvider timeProvider,
        ILogger<RefreshSessionHandler> logger)
    {
        _dbContext = dbContext;
        _refreshTokens = refreshTokens;
        _sessionIssuer = sessionIssuer;
        _timeProvider = timeProvider;
        _rotationGrace = TimeSpan.FromSeconds(options.Value.RefreshRotationGraceSeconds);
        _logger = logger;
    }

    public async Task<RefreshSessionResult> Handle(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RefreshSessionResult.Rejected(RefreshRejection.MissingToken);
        }

        var now = _timeProvider.GetUtcNow();
        var hash = _refreshTokens.Hash(command.RefreshToken);

        var presented = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (presented is null)
        {
            return RefreshSessionResult.Rejected(RefreshRejection.UnknownToken);
        }

        if (presented.IsExpired(now))
        {
            return RefreshSessionResult.Rejected(RefreshRejection.ExpiredToken);
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == presented.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(presented.FamilyId, now, cancellationToken);

            return RefreshSessionResult.Rejected(RefreshRejection.AccountUnavailable);
        }

        var replacementId = Guid.CreateVersion7();

        var claimed = await _dbContext.RefreshTokens
            .Where(candidate => candidate.Id == presented.Id && candidate.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.RevokedAtUtc, now)
                    .SetProperty(candidate => candidate.ReplacedByTokenId, replacementId),
                cancellationToken);

        if (claimed == 0)
        {
            return await ResolveLostClaimAsync(presented.Id, cancellationToken);
        }

        var session = await _sessionIssuer.ContinueAsync(user, presented.FamilyId, replacementId, cancellationToken);

        _logger.LogInformation(
            "Session refreshed for user {UserId} on family {FamilyId}",
            user.Id,
            presented.FamilyId);

        return RefreshSessionResult.Accepted(
            new AuthenticatedUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                session.AccessToken.ExpiresAtUtc),
            session);
    }

    private async Task<RefreshSessionResult> ResolveLostClaimAsync(
        Guid tokenId,
        CancellationToken cancellationToken)
    {
        var current = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == tokenId, cancellationToken);

        if (current is null)
        {
            return RefreshSessionResult.Rejected(RefreshRejection.UnknownToken);
        }

        var nowUtc = _timeProvider.GetUtcNow();

        if (current.WasRotatedWithin(nowUtc, _rotationGrace))
        {
            _logger.LogInformation(
                "Refresh token for user {UserId} was rotated moments ago on family {FamilyId}; "
                + "treating the second presentation as a concurrent refresh rather than reuse",
                current.UserId,
                current.FamilyId);

            return RefreshSessionResult.Rejected(RefreshRejection.ConcurrentRotation);
        }

        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}; revoking session family {FamilyId}",
            current.UserId,
            current.FamilyId);

        await RevokeFamilyAsync(current.FamilyId, nowUtc, cancellationToken);

        return RefreshSessionResult.Rejected(RefreshRejection.ReusedToken);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        await _dbContext.RefreshTokens
            .Where(candidate => candidate.FamilyId == familyId && candidate.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.RevokedAtUtc, nowUtc),
                cancellationToken);
    }
}
