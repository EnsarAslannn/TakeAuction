using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.Features.Auth.GetHubTicket;

public sealed class GetHubTicketHandler : IRequestHandler<GetHubTicketQuery, HubTicketResponse?>
{
    private readonly AppDbContext _dbContext;
    private readonly IJwtTokenGenerator _tokens;

    public GetHubTicketHandler(AppDbContext dbContext, IJwtTokenGenerator tokens)
    {
        _dbContext = dbContext;
        _tokens = tokens;
    }

    public async Task<HubTicketResponse?> Handle(GetHubTicketQuery query, CancellationToken cancellationToken)
    {
        if (query.UserId == Guid.Empty)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == query.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var ticket = _tokens.GenerateHubTicket(user);

        return new HubTicketResponse(ticket.Value, ticket.ExpiresAtUtc);
    }
}
