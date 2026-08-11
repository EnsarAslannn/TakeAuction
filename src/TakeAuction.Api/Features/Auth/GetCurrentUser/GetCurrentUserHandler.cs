using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Auth.GetCurrentUser;

public sealed class GetCurrentUserHandler : IRequestHandler<GetCurrentUserQuery, CurrentUserResponse?>
{
    private readonly AppDbContext _dbContext;

    public GetCurrentUserHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<CurrentUserResponse?> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken) =>
        _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == query.UserId && user.IsActive)
            .Select(user => new CurrentUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                user.CreatedAtUtc,
                user.LastLoginAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
