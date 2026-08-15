using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionAntiSnipeTests
{
    private const int Window = 60;
    private const int Extension = 60;

    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Guid Bidder = Guid.CreateVersion7();
    private static readonly Guid OtherBidder = Guid.CreateVersion7();

    [Fact]
    public void A_bid_well_before_the_close_leaves_the_end_alone()
    {
        var auction = ClosingAuction();
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 150m, originalEnd.AddSeconds(-Window - 1));

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.Extended);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void A_bid_inside_the_window_pushes_the_close_out()
    {
        var auction = ClosingAuction();
        var originalEnd = auction.EndsAtUtc;
        var snipedAt = originalEnd.AddSeconds(-5);

        var outcome = auction.PlaceBid(Bidder, 150m, snipedAt);

        Assert.True(outcome.Extended);
        Assert.Equal(snipedAt.AddSeconds(Extension), auction.EndsAtUtc);
    }

    [Fact]
    public void A_bid_landing_exactly_on_the_edge_of_the_window_is_inside_it()
    {
        // The extension has to outlast the window for the boundary to be observable at all:
        // when the two are equal, a bid on the edge lands on the close it already had.
        var auction = ClosingAuction(window: Window, extension: Window * 2);
        var snipedAt = auction.EndsAtUtc.AddSeconds(-Window);

        var outcome = auction.PlaceBid(Bidder, 150m, snipedAt);

        Assert.True(outcome.Extended);
        Assert.Equal(snipedAt.AddSeconds(Window * 2), auction.EndsAtUtc);
    }

    [Fact]
    public void A_bid_a_hair_outside_the_window_is_left_alone()
    {
        var auction = ClosingAuction(window: Window, extension: Window * 2);
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 150m, originalEnd.AddSeconds(-Window).AddTicks(-1));

        Assert.False(outcome.Extended);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void Every_snipe_buys_the_room_the_same_reply_window()
    {
        var auction = ClosingAuction();

        var first = auction.EndsAtUtc.AddSeconds(-1);
        auction.PlaceBid(Bidder, 150m, first);
        Assert.Equal(first.AddSeconds(Extension), auction.EndsAtUtc);

        var second = auction.EndsAtUtc.AddSeconds(-1);
        auction.PlaceBid(OtherBidder, 200m, second);
        Assert.Equal(second.AddSeconds(Extension), auction.EndsAtUtc);
    }

    [Fact]
    public void An_extension_can_only_ever_move_the_close_later()
    {
        // A short extension on a lot that still has more time left than the extension would
        // otherwise pull the close towards us and cut the auction short.
        var auction = ClosingAuction(window: 3600, extension: 5);
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 150m, originalEnd.AddSeconds(-600));

        Assert.False(outcome.Extended);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void A_lot_listed_without_a_soft_close_closes_on_time()
    {
        var auction = ClosingAuction(window: 0, extension: 0);
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 150m, originalEnd.AddSeconds(-1));

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.Extended);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void A_rejected_bid_never_buys_extra_time()
    {
        var auction = ClosingAuction();
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 1m, originalEnd.AddSeconds(-1));

        Assert.Equal(BidRejection.BidTooLow, outcome.Rejection);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void A_bid_that_arrives_after_the_close_cannot_reopen_the_lot()
    {
        var auction = ClosingAuction();
        var originalEnd = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bidder, 150m, originalEnd);

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
        Assert.Equal(originalEnd, auction.EndsAtUtc);
    }

    [Fact]
    public void An_extended_lot_is_no_longer_due_to_close()
    {
        var auction = ClosingAuction();
        var originalEnd = auction.EndsAtUtc;

        auction.PlaceBid(Bidder, 150m, originalEnd.AddSeconds(-1));

        Assert.False(auction.End(originalEnd));
        Assert.Equal(AuctionStatus.Active, auction.Status);

        Assert.True(auction.End(auction.EndsAtUtc));
        Assert.Equal(AuctionStatus.Ended, auction.Status);
    }

    [Fact]
    public void The_rules_are_frozen_onto_the_lot_when_it_is_listed()
    {
        var auction = ClosingAuction(window: 30, extension: 90);

        Assert.Equal(30, auction.AntiSnipeWindowSeconds);
        Assert.Equal(90, auction.AntiSnipeExtensionSeconds);
    }

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(60, -1)]
    public void Negative_soft_close_settings_are_a_programming_error(int window, int extension)
    {
        Assert.Throws<ArgumentException>(() => ClosingAuction(window: window, extension: extension));
    }

    private static Auction ClosingAuction(int window = Window, int extension = Extension) => Auction.Create(
        Seller,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        100m,
        5m,
        TestHarness.Now,
        TestHarness.Now.AddHours(1),
        TestHarness.Now,
        antiSnipeWindowSeconds: window,
        antiSnipeExtensionSeconds: extension);
}
