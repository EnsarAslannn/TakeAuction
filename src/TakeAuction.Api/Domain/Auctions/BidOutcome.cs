namespace TakeAuction.Api.Domain.Auctions;

public enum BidRejection
{
    None = 0,
    AuctionNotFound = 1,
    AuctionNotOpen = 2,
    SellerCannotBid = 3,
    BidTooLow = 4,
    ConcurrencyConflict = 5
}

public sealed record BidOutcome(BidRejection Rejection, Bid? Bid)
{
    public bool Succeeded => Rejection == BidRejection.None;

    public static BidOutcome Accepted(Bid bid) => new(BidRejection.None, bid);

    public static BidOutcome Rejected(BidRejection rejection) => new(rejection, null);
}
