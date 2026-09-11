using MediatR;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Watchlist.UnwatchAuction;

public sealed class UnwatchAuctionHandler : IRequestHandler<UnwatchAuctionCommand, bool>
{
    private readonly AppDbContext _dbContext;

    public UnwatchAuctionHandler(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<bool> Handle(UnwatchAuctionCommand command, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.AuctionWatches
            .Where(watch => watch.UserId == command.UserId && watch.AuctionId == command.AuctionId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }
}
