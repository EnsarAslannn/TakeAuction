namespace TakeAuction.Api.Domain.Notifications;

public sealed class Notification
{
    public const int MaxAuctionTitleLength = 200;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationKind Kind { get; private set; }
    public Guid AuctionId { get; private set; }
    public string AuctionTitle { get; private set; } = null!;
    public decimal? Amount { get; private set; }
    public DateTimeOffset AuctionEndsAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }

    private Notification() { }

    public static Notification Create(
        Guid userId,
        NotificationKind kind,
        Guid auctionId,
        string auctionTitle,
        decimal? amount,
        DateTimeOffset auctionEndsAtUtc,
        DateTimeOffset nowUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A recipient is required.", nameof(userId));
        }

        if (auctionId == Guid.Empty)
        {
            throw new ArgumentException("An auction is required.", nameof(auctionId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown notification kind.");
        }

        if (string.IsNullOrWhiteSpace(auctionTitle))
        {
            throw new ArgumentException("The auction title is required.", nameof(auctionTitle));
        }

        var title = auctionTitle.Trim();

        return new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            AuctionId = auctionId,
            AuctionTitle = title.Length > MaxAuctionTitleLength ? title[..MaxAuctionTitleLength] : title,
            Amount = amount,
            AuctionEndsAtUtc = auctionEndsAtUtc.ToUniversalTime(),
            CreatedAtUtc = nowUtc
        };
    }
}
