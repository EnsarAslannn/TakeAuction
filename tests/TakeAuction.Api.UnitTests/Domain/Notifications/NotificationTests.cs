using TakeAuction.Api.Domain.Notifications;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Notifications;

public sealed class NotificationTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid AuctionId = Guid.CreateVersion7();

    [Fact]
    public void A_new_notification_is_unread()
    {
        var notification = Create();

        Assert.Null(notification.ReadAtUtc);
        Assert.Equal(TestHarness.Now, notification.CreatedAtUtc);
        Assert.NotEqual(Guid.Empty, notification.Id);
    }

    [Fact]
    public void Keeps_the_title_trimmed_and_within_the_column()
    {
        var longTitle = "  " + new string('x', Notification.MaxAuctionTitleLength + 40) + "  ";

        var notification = Create(title: longTitle);

        Assert.Equal(Notification.MaxAuctionTitleLength, notification.AuctionTitle.Length);
        Assert.DoesNotContain(' ', notification.AuctionTitle);
    }

    [Fact]
    public void Stores_the_close_in_utc()
    {
        var endsAt = new DateTimeOffset(2026, 9, 1, 15, 0, 0, TimeSpan.FromHours(3));

        var notification = Notification.Create(
            UserId, NotificationKind.AuctionWon, AuctionId, "Lot", 10m, endsAt, TestHarness.Now);

        Assert.Equal(TimeSpan.Zero, notification.AuctionEndsAtUtc.Offset);
        Assert.Equal(endsAt, notification.AuctionEndsAtUtc);
    }

    [Fact]
    public void Refuses_a_notification_without_a_recipient() =>
        Assert.Throws<ArgumentException>(() => Notification.Create(
            Guid.Empty, NotificationKind.AuctionWon, AuctionId, "Lot", 10m, TestHarness.Now, TestHarness.Now));

    [Fact]
    public void Refuses_a_notification_without_an_auction() =>
        Assert.Throws<ArgumentException>(() => Notification.Create(
            UserId, NotificationKind.AuctionWon, Guid.Empty, "Lot", 10m, TestHarness.Now, TestHarness.Now));

    [Fact]
    public void Refuses_an_untitled_notification() =>
        Assert.Throws<ArgumentException>(() => Create(title: "   "));

    [Fact]
    public void Refuses_a_kind_it_does_not_know() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Notification.Create(
            UserId, (NotificationKind)99, AuctionId, "Lot", null, TestHarness.Now, TestHarness.Now));

    private static Notification Create(string title = "Rare stamp collection") =>
        Notification.Create(
            UserId,
            NotificationKind.AuctionSold,
            AuctionId,
            title,
            250m,
            TestHarness.Now,
            TestHarness.Now);
}
