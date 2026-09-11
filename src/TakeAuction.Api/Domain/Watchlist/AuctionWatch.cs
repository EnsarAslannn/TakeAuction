namespace TakeAuction.Api.Domain.Watchlist;

public sealed class AuctionWatch
{
    public Guid UserId { get; private set; }
    public Guid AuctionId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosingSoonNotifiedAtUtc { get; private set; }

    private AuctionWatch() { }

    public static AuctionWatch Start(Guid userId, Guid auctionId, DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A watcher is required.", nameof(userId));
        }

        if (auctionId == Guid.Empty)
        {
            throw new ArgumentException("An auction is required.", nameof(auctionId));
        }

        return new AuctionWatch
        {
            UserId = userId,
            AuctionId = auctionId,
            CreatedAtUtc = nowUtc
        };
    }
}
