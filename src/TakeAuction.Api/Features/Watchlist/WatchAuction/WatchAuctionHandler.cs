using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Watchlist;

namespace TakeAuction.Api.Features.Watchlist.WatchAuction;

public sealed class WatchAuctionHandler : IRequestHandler<WatchAuctionCommand, WatchAuctionResult>
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public WatchAuctionHandler(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<WatchAuctionResult> Handle(WatchAuctionCommand command, CancellationToken cancellationToken)
    {
        var status = await _dbContext.Auctions
            .AsNoTracking()
            .Where(auction => auction.Id == command.AuctionId)
            .Select(auction => (AuctionStatus?)auction.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return WatchAuctionResult.AuctionNotFound;
        }

        if (status is AuctionStatus.Ended or AuctionStatus.Cancelled)
        {
            return WatchAuctionResult.AuctionClosed;
        }

        var alreadyWatching = await _dbContext.AuctionWatches
            .AsNoTracking()
            .AnyAsync(
                watch => watch.UserId == command.UserId && watch.AuctionId == command.AuctionId,
                cancellationToken);

        if (alreadyWatching)
        {
            return WatchAuctionResult.Watching;
        }

        _dbContext.AuctionWatches.Add(AuctionWatch.Start(command.UserId, command.AuctionId, _timeProvider.GetUtcNow()));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return WatchAuctionResult.Watching;
        }

        return WatchAuctionResult.Watching;
    }
}
