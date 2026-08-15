namespace TakeAuction.Api.Domain.Auctions;

public sealed class Bid
{
    public const int MaxIdempotencyKeyLength = 128;

    public Guid Id { get; private set; }
    public Guid AuctionId { get; private set; }
    public Guid BidderId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset PlacedAtUtc { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private Bid() { }

    internal static Bid Create(
        Guid auctionId,
        Guid bidderId,
        decimal amount,
        DateTimeOffset placedAtUtc,
        string? idempotencyKey = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AuctionId = auctionId,
            BidderId = bidderId,
            Amount = amount,
            PlacedAtUtc = placedAtUtc,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        };
}
