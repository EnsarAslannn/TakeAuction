using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.Login;

public sealed class LoginHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ILogger<LoginHandler> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        if (user is null)
        {
            // Hash anyway so a missing account and a wrong password take comparable time.
            _passwordHasher.Verify(DummyHash, command.Password);

            return LoginResult.Rejected(LoginRejection.InvalidCredentials);
        }

        var outcome = _passwordHasher.Verify(user.PasswordHash, command.Password);

        if (outcome == PasswordVerificationOutcome.Failed)
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);

            return LoginResult.Rejected(LoginRejection.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return LoginResult.Rejected(LoginRejection.AccountDisabled);
        }

        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.ChangePassword(_passwordHasher.Hash(command.Password));
        }

        user.RecordLogin();

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} signed in", user.Id);

        var accessToken = _tokenGenerator.Generate(user);

        return LoginResult.Accepted(
            new AuthenticatedUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                accessToken.ExpiresAtUtc),
            accessToken);
    }

    private const string DummyHash =
        "AQAAAAIAAYagAAAAEHxSTQBpQ0Vm0KUOSyEMhkKUUb0aLVpVFtcZmM0OcJHhCUqRHVU7uQsMlpBnJPjkFA==";
}
