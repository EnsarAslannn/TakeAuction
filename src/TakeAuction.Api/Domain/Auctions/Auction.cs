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

    public decimal LeadingMaxAmount { get; private set; }

    public int BidCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public int AntiSnipeWindowSeconds { get; private set; }

    public int AntiSnipeExtensionSeconds { get; private set; }

    public decimal MinimumAcceptableBid =>
        BidCount == 0 ? StartingPrice : CurrentPrice + MinimumBidIncrement;

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

        var leaderMax = Math.Max(LeadingMaxAmount, CurrentPrice);

        if (maxAmount > leaderMax)
        {
            var price = Math.Min(maxAmount, leaderMax + MinimumBidIncrement);
            var bid = Bid.Create(Id, bidderId, price, maxAmount, nowUtc, idempotencyKey: idempotencyKey);

            CurrentPrice = price;
            LeadingBidderId = bidderId;
            LeadingMaxAmount = maxAmount;
            BidCount++;

            return BidOutcome.Accepted(bid, extended: ExtendIfSniped(nowUtc));
        }

        var challenge = Bid.Create(Id, bidderId, maxAmount, maxAmount, nowUtc, idempotencyKey: idempotencyKey);
        var answerPrice = Math.Min(leaderMax, maxAmount + MinimumBidIncrement);
        var answer = Bid.Create(Id, leaderId, answerPrice, leaderMax, nowUtc, isAutomatic: true);

        CurrentPrice = answerPrice;
        BidCount += 2;

        return BidOutcome.Accepted(challenge, answer, ExtendIfSniped(nowUtc));
    }

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
