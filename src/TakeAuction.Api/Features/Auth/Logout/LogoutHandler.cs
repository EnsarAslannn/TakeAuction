using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand, LogoutResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IRefreshTokenGenerator _refreshTokens;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        AppDbContext dbContext,
        IRefreshTokenGenerator refreshTokens,
        TimeProvider timeProvider,
        ILogger<LogoutHandler> logger)
    {
        _dbContext = dbContext;
        _refreshTokens = refreshTokens;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<LogoutResult> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return new LogoutResult(SessionRevoked: false);
        }

        var hash = _refreshTokens.Hash(command.RefreshToken);

        var familyId = await _dbContext.RefreshTokens
            .Where(token => token.TokenHash == hash)
            .Select(token => (Guid?)token.FamilyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (familyId is null)
        {
            return new LogoutResult(SessionRevoked: false);
        }

        var revoked = await _dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, _timeProvider.GetUtcNow()),
                cancellationToken);

        _logger.LogInformation("Signed out session family {FamilyId}, revoking {Revoked} token(s)", familyId, revoked);

        return new LogoutResult(SessionRevoked: revoked > 0);
    }
}
