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

    /// <summary>
    /// The ceiling behind the leading bid. It is what lets the house answer a challenger
    /// without asking the leader, and it is never published — a rival who knew it would know
    /// the exact figure that takes the lot.
    /// </summary>
    public decimal LeadingMaxAmount { get; private set; }

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

    /// <summary>
    /// The leader is not bidding against the public price — they already hold it. What they
    /// have to clear is their own ceiling, by the same increment everyone else clears.
    /// </summary>
    public decimal MinimumAcceptableBidFor(Guid bidderId) =>
        bidderId == LeadingBidderId
            ? LeadingMaxAmount + MinimumBidIncrement
            : MinimumAcceptableBid;

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

    /// <summary>
    /// The amount is the most the bidder is prepared to pay, not what they pay. The house bids
    /// on their behalf only as far as it takes to lead, so a bidder never pays more than one
    /// increment over the next-highest ceiling — and never has to sit at the screen defending
    /// their lot by hand.
    /// </summary>
    public BidOutcome PlaceBid(
        Guid bidderId,
        decimal maxAmount,
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

        if (bidderId == LeadingBidderId)
        {
            return RaiseOwnCeiling(bidderId, maxAmount, nowUtc, idempotencyKey);
        }

        if (maxAmount < MinimumAcceptableBid)
        {
            return BidOutcome.Rejected(BidRejection.BidTooLow);
        }

        Status = AuctionStatus.Active;

        return LeadingBidderId is null
            ? OpenTheBidding(bidderId, maxAmount, nowUtc, idempotencyKey)
            : Challenge(bidderId, maxAmount, nowUtc, idempotencyKey);
    }

    /// <summary>
    /// The first bidder pays the asking price no matter how high they were prepared to go.
    /// There is nobody to bid against yet, and a sealed maximum that spent itself the moment
    /// it was submitted would not be a maximum at all.
    /// </summary>
    private BidOutcome OpenTheBidding(
        Guid bidderId,
        decimal maxAmount,
        DateTimeOffset nowUtc,
        string? idempotencyKey)
    {
        var bid = Bid.Create(Id, bidderId, StartingPrice, maxAmount, nowUtc, idempotencyKey: idempotencyKey);

        CurrentPrice = StartingPrice;
        LeadingBidderId = bidderId;
        LeadingMaxAmount = maxAmount;
        BidCount++;

        return BidOutcome.Accepted(bid, extended: ExtendIfSniped(nowUtc));
    }

    private BidOutcome Challenge(
        Guid bidderId,
        decimal maxAmount,
        DateTimeOffset nowUtc,
        string? idempotencyKey)
    {
        var leaderId = LeadingBidderId!.Value;

        // A leader's ceiling is never below the price they are already holding — the bidding
        // rules cannot produce that state, but a row written before they existed can, and
        // reading such a ceiling literally would hand the lot to the next challenger for a
        // single increment. Standing at the price is the least the leader can be said to have
        // agreed to.
        var leaderMax = Math.Max(LeadingMaxAmount, CurrentPrice);

        if (maxAmount > leaderMax)
        {
            // The challenger takes the lot, but only pays what it took to pass the ceiling
            // they beat — one increment over it, or their own maximum if that comes first.
            var price = Math.Min(maxAmount, leaderMax + MinimumBidIncrement);
            var bid = Bid.Create(Id, bidderId, price, maxAmount, nowUtc, idempotencyKey: idempotencyKey);

            CurrentPrice = price;
            LeadingBidderId = bidderId;
            LeadingMaxAmount = maxAmount;
            BidCount++;

            return BidOutcome.Accepted(bid, extended: ExtendIfSniped(nowUtc));
        }

        // The leader's ceiling holds, so the house answers for them. A tie goes to the
        // incumbent: matching a maximum is not beating it, and the earlier commitment stands.
        var challenge = Bid.Create(Id, bidderId, maxAmount, maxAmount, nowUtc, idempotencyKey: idempotencyKey);
        var answerPrice = Math.Min(leaderMax, maxAmount + MinimumBidIncrement);
        var answer = Bid.Create(Id, leaderId, answerPrice, leaderMax, nowUtc, isAutomatic: true);

        CurrentPrice = answerPrice;
        BidCount += 2;

        return BidOutcome.Accepted(challenge, answer, ExtendIfSniped(nowUtc));
    }

    /// <summary>
    /// Raising your own ceiling while you lead moves nothing anyone can see, so it buys no
    /// extra time either. Otherwise a leader could hold a lot open indefinitely by nudging
    /// their own maximum every time the clock ran down.
    /// </summary>
    private BidOutcome RaiseOwnCeiling(
        Guid bidderId,
        decimal maxAmount,
        DateTimeOffset nowUtc,
        string? idempotencyKey)
    {
        if (maxAmount < MinimumAcceptableBidFor(bidderId))
        {
            return BidOutcome.Rejected(BidRejection.BidTooLow);
        }

        var bid = Bid.Create(Id, bidderId, CurrentPrice, maxAmount, nowUtc, idempotencyKey: idempotencyKey);

        LeadingMaxAmount = maxAmount;
        BidCount++;

        return BidOutcome.Accepted(bid);
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

    /// <summary>
    /// A lot can be withdrawn only while nobody has committed to it. Once a bid is in, the
    /// seller no longer owns the outcome on their own: bidders have ceilings riding on it, and
    /// letting a seller pull a lot they did not like the look of is exactly the thing a bidder
    /// has to be able to trust will not happen.
    /// </summary>
    public CancelOutcome Cancel(Guid sellerId, DateTimeOffset nowUtc)
    {
        if (sellerId == Guid.Empty)
        {
            throw new ArgumentException("Seller id is required.", nameof(sellerId));
        }

        if (sellerId != SellerId)
        {
            return CancelOutcome.Rejected(CancelRejection.NotTheSeller);
        }

        if (Status is AuctionStatus.Cancelled)
        {
            return CancelOutcome.Rejected(CancelRejection.AlreadyCancelled);
        }

        if (Status is AuctionStatus.Ended || nowUtc >= EndsAtUtc)
        {
            return CancelOutcome.Rejected(CancelRejection.AlreadyClosed);
        }

        if (BidCount > 0)
        {
            return CancelOutcome.Rejected(CancelRejection.AlreadyBidOn);
        }

        Status = AuctionStatus.Cancelled;

        return CancelOutcome.Accepted();
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
