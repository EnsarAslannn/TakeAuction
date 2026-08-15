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

public sealed record BidOutcome(
    BidRejection Rejection,
    Bid? Bid,
    Bid? AutomaticBid = null,
    bool Extended = false)
{
    public bool Succeeded => Rejection == BidRejection.None;

    /// <summary>
    /// The bid that left the lot at the price it now shows — the leader's automatic answer
    /// when their proxy held the line, otherwise the one that was submitted.
    /// </summary>
    public Bid? PriceSetter => AutomaticBid ?? Bid;

    public static BidOutcome Accepted(Bid bid, Bid? automaticBid = null, bool extended = false) =>
        new(BidRejection.None, bid, automaticBid, extended);

    public static BidOutcome Rejected(BidRejection rejection) => new(rejection, null);
}
