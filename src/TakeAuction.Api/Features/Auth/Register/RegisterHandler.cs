using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Auth.Register;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ILogger<RegisterHandler> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(RegisterCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        var alreadyExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, cancellationToken);

        if (alreadyExists)
        {
            return RegisterResult.Conflict();
        }

        var role = Enum.Parse<UserRole>(command.Role, ignoreCase: true);

        var user = User.Create(
            email,
            command.DisplayName,
            _passwordHasher.Hash(command.Password),
            role);

        await _dbContext.Users.AddAsync(user, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            _dbContext.Entry(user).State = EntityState.Detached;

            return RegisterResult.Conflict();
        }

        _logger.LogInformation("User {UserId} registered with role {Role}", user.Id, user.Role);

        var accessToken = _tokenGenerator.Generate(user);

        return RegisterResult.Created(
            new AuthenticatedUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                accessToken.ExpiresAtUtc),
            accessToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
