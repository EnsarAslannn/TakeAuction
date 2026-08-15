namespace TakeAuction.Api.Domain.Auctions;

public sealed class Auction
{
    public const int DefaultAntiSnipeWindowSeconds = 60;

    public const int DefaultAntiSnipeExtensionSeconds = 60;

    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string? ImageUrl { get; private set; }
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

    /// <summary>
    /// The soft-close rules are stored on the lot rather than read from configuration on every
    /// bid. They are part of the deal a bidder accepted when they joined this lot, so changing
    /// the defaults must not move the goalposts under an auction that is already running.
    /// </summary>
    public int AntiSnipeWindowSeconds { get; private set; }

    public int AntiSnipeExtensionSeconds { get; private set; }

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
        DateTimeOffset nowUtc,
        string? imageUrl = null,
        int antiSnipeWindowSeconds = DefaultAntiSnipeWindowSeconds,
        int antiSnipeExtensionSeconds = DefaultAntiSnipeExtensionSeconds)
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

        if (antiSnipeWindowSeconds < 0)
        {
            throw new ArgumentException("Anti-snipe window cannot be negative.", nameof(antiSnipeWindowSeconds));
        }

        if (antiSnipeExtensionSeconds < 0)
        {
            throw new ArgumentException("Anti-snipe extension cannot be negative.", nameof(antiSnipeExtensionSeconds));
        }

        return new Auction
        {
            Id = Guid.CreateVersion7(),
            SellerId = sellerId,
            Title = title.Trim(),
            Description = description.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            StartingPrice = startingPrice,
            CurrentPrice = startingPrice,
            MinimumBidIncrement = minimumBidIncrement,
            StartsAtUtc = startsAtUtc.ToUniversalTime(),
            EndsAtUtc = endsAtUtc.ToUniversalTime(),
            Status = startsAtUtc > nowUtc ? AuctionStatus.Scheduled : AuctionStatus.Active,
            CreatedAtUtc = nowUtc,
            AntiSnipeWindowSeconds = antiSnipeWindowSeconds,
            AntiSnipeExtensionSeconds = antiSnipeExtensionSeconds
        };
    }

    public BidOutcome PlaceBid(
        Guid bidderId,
        decimal amount,
        DateTimeOffset nowUtc,
        string? idempotencyKey = null)
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

        var bid = Bid.Create(Id, bidderId, amount, nowUtc, idempotencyKey);

        Status = AuctionStatus.Active;
        CurrentPrice = amount;
        LeadingBidderId = bidderId;
        BidCount++;

        return BidOutcome.Accepted(bid, ExtendIfSniped(nowUtc));
    }

    /// <summary>
    /// A bid landing in the closing seconds pushes the end out, so winning cannot be a matter
    /// of arriving late enough that nobody has time to answer. The clock is set from the bid,
    /// not added to the old end: every snipe buys the room the same fixed reply window, and a
    /// lot only settles once a bid goes unanswered.
    /// </summary>
    private bool ExtendIfSniped(DateTimeOffset nowUtc)
    {
        if (AntiSnipeWindowSeconds <= 0 || AntiSnipeExtensionSeconds <= 0)
        {
            return false;
        }

        if (EndsAtUtc - nowUtc > TimeSpan.FromSeconds(AntiSnipeWindowSeconds))
        {
            return false;
        }

        var extendedTo = nowUtc.AddSeconds(AntiSnipeExtensionSeconds);

        if (extendedTo <= EndsAtUtc)
        {
            return false;
        }

        EndsAtUtc = extendedTo;

        return true;
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
