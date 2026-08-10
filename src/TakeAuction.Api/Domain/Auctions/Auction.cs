namespace TakeAuction.Api.Domain.Auctions;

public sealed class Auction
{
    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public decimal StartingPrice { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal MinimumBidIncrement { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset EndsAtUtc { get; private set; }
    public AuctionStatus Status { get; private set; }
    public Guid? LeadingBidderId { get; private set; }
    public int BidCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public decimal MinimumAcceptableBid =>
        BidCount == 0 ? StartingPrice : CurrentPrice + MinimumBidIncrement;

    private Auction() { }

    public static Auction Create(
        Guid sellerId,
        string title,
        string description,
        decimal startingPrice,
        decimal minimumBidIncrement,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset nowUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (endsAtUtc <= startsAtUtc)
        {
            throw new ArgumentException("Auction must end after it starts.", nameof(endsAtUtc));
        }

        if (startingPrice <= 0m)
        {
            throw new ArgumentException("Starting price must be greater than zero.", nameof(startingPrice));
        }

        if (minimumBidIncrement <= 0m)
        {
            throw new ArgumentException("Minimum bid increment must be greater than zero.", nameof(minimumBidIncrement));
        }

        return new Auction
        {
            Id = Guid.CreateVersion7(),
            SellerId = sellerId,
            Title = title.Trim(),
            Description = description.Trim(),
            StartingPrice = startingPrice,
            CurrentPrice = startingPrice,
            MinimumBidIncrement = minimumBidIncrement,
            StartsAtUtc = startsAtUtc.ToUniversalTime(),
            EndsAtUtc = endsAtUtc.ToUniversalTime(),
            Status = startsAtUtc > nowUtc ? AuctionStatus.Scheduled : AuctionStatus.Active,
            CreatedAtUtc = nowUtc
        };
    }

    public BidOutcome PlaceBid(Guid bidderId, decimal amount, DateTimeOffset nowUtc)
    {
        if (bidderId == Guid.Empty)
        {
            throw new ArgumentException("Bidder id is required.", nameof(bidderId));
        }

        if (bidderId == SellerId)
        {
            return BidOutcome.Rejected(BidRejection.SellerCannotBid);
        }

        if (Status is AuctionStatus.Ended or AuctionStatus.Cancelled)
        {
            return BidOutcome.Rejected(BidRejection.AuctionNotOpen);
        }

        if (nowUtc < StartsAtUtc || nowUtc >= EndsAtUtc)
        {
            return BidOutcome.Rejected(BidRejection.AuctionNotOpen);
        }

        if (amount < MinimumAcceptableBid)
        {
            return BidOutcome.Rejected(BidRejection.BidTooLow);
        }

        var bid = Bid.Create(Id, bidderId, amount, nowUtc);

        Status = AuctionStatus.Active;
        CurrentPrice = amount;
        LeadingBidderId = bidderId;
        BidCount++;

        return BidOutcome.Accepted(bid);
    }

    public bool End(DateTimeOffset nowUtc)
    {
        if (Status is AuctionStatus.Ended or AuctionStatus.Cancelled)
        {
            return false;
        }

        if (nowUtc < EndsAtUtc)
        {
            return false;
        }

        Status = AuctionStatus.Ended;

        return true;
    }
}
