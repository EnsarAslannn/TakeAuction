namespace TakeAuction.Api.Domain.Auctions;

public enum CancelRejection
{
    None = 0,
    AuctionNotFound = 1,
    NotTheSeller = 2,
    AlreadyBidOn = 3,
    AlreadyClosed = 4,
    AlreadyCancelled = 5,
    ConcurrencyConflict = 6
}

public sealed record CancelOutcome(CancelRejection Rejection)
{
    public bool Succeeded => Rejection == CancelRejection.None;

    public static CancelOutcome Accepted() => new(CancelRejection.None);

    public static CancelOutcome Rejected(CancelRejection rejection) => new(rejection);
}
