namespace TakeAuction.Api.Domain.Auctions;

public sealed class Bid
{
    public const int MaxIdempotencyKeyLength = 128;

    public Guid Id { get; private set; }
    public Guid AuctionId { get; private set; }
    public Guid BidderId { get; private set; }

    /// <summary>
    /// What the lot actually stood at because of this bid — the public price.
    /// </summary>
    public decimal Amount { get; private set; }

    /// <summary>
    /// The most this bidder was willing to go at the time. Never shown to rivals: knowing a
    /// leader's ceiling is knowing exactly what it takes to beat them, which is the one thing a
    /// sealed maximum exists to keep private.
    /// </summary>
    public decimal MaxAmount { get; private set; }

    /// <summary>
    /// True when the house placed this bid on a leader's behalf to answer a challenger,
    /// rather than the bidder submitting it.
    /// </summary>
    public bool IsAutomatic { get; private set; }

    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private Bid() { }

    internal static Bid Create(
        Guid auctionId,
        Guid bidderId,
        decimal amount,
        decimal maxAmount,
        DateTimeOffset placedAtUtc,
        bool isAutomatic = false,
        string? idempotencyKey = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AuctionId = auctionId,
            BidderId = bidderId,
            Amount = amount,
            MaxAmount = maxAmount,
            IsAutomatic = isAutomatic,
            PlacedAtUtc = placedAtUtc,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        };
}
