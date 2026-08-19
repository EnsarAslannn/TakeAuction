using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.RefreshSession;

public sealed class RefreshSessionHandler : IRequestHandler<RefreshSessionCommand, RefreshSessionResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly ISessionIssuer _sessionIssuer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshSessionHandler> _logger;

    public RefreshSessionHandler(
        AppDbContext dbContext,
        IRefreshTokenGenerator refreshTokens,
        ISessionIssuer sessionIssuer,
        TimeProvider timeProvider,
        ILogger<RefreshSessionHandler> logger)
    {
        _dbContext = dbContext;
        _refreshTokens = refreshTokens;
        _sessionIssuer = sessionIssuer;
        _timeProvider = timeProvider;
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

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            return RefreshSessionResult.Rejected(RefreshRejection.UnknownToken);
        }

        if (token.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}; revoking session family {FamilyId}",
                token.UserId,
                token.FamilyId);

            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);

            return RefreshSessionResult.Rejected(RefreshRejection.ReusedToken);
        }

        if (token.IsExpired(now))
        {
            return RefreshSessionResult.Rejected(RefreshRejection.ExpiredToken);
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Id == token.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);

            return RefreshSessionResult.Rejected(RefreshRejection.AccountUnavailable);
        }

        var session = await _sessionIssuer.RotateAsync(user, token, cancellationToken);

        _logger.LogInformation(
            "Session refreshed for user {UserId} on family {FamilyId}",
            user.Id,
            token.FamilyId);

        return RefreshSessionResult.Accepted(
            new AuthenticatedUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                session.AccessToken.ExpiresAtUtc),
            session);
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
