using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionOpeningTests
{
    private static readonly Guid SellerId = Guid.CreateVersion7();

    [Fact]
    public void Opens_a_scheduled_auction_once_its_start_time_arrives()
    {
        var auction = ScheduledAuction();

        var opened = auction.Open(auction.StartsAtUtc);

        Assert.True(opened);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Leaves_a_scheduled_auction_alone_before_its_start_time()
    {
        var auction = ScheduledAuction();

        var opened = auction.Open(auction.StartsAtUtc.AddSeconds(-1));

        Assert.False(opened);
        Assert.Equal(AuctionStatus.Scheduled, auction.Status);
    }

    [Fact]
    public void Opening_an_already_open_auction_is_a_no_op()
    {
        var auction = ScheduledAuction();
        auction.Open(auction.StartsAtUtc);

        var openedAgain = auction.Open(auction.StartsAtUtc.AddMinutes(1));

        Assert.False(openedAgain);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Refuses_to_open_a_lot_whose_window_has_already_elapsed()
    {
        var auction = ScheduledAuction();

        var opened = auction.Open(auction.EndsAtUtc);

        Assert.False(opened);
        Assert.Equal(AuctionStatus.Scheduled, auction.Status);
    }

    [Fact]
    public void Refuses_to_reopen_a_cancelled_lot()
    {
        var auction = ScheduledAuction();
        auction.Cancel(SellerId, TestHarness.Now);

        var opened = auction.Open(auction.StartsAtUtc);

        Assert.False(opened);
        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
    }

    [Fact]
    public void An_opened_lot_takes_bids_at_its_starting_price()
    {
        var auction = ScheduledAuction();
        auction.Open(auction.StartsAtUtc);

        var outcome = auction.PlaceBid(Guid.CreateVersion7(), 100m, auction.StartsAtUtc);

        Assert.True(outcome.Succeeded);
        Assert.Equal(100m, auction.CurrentPrice);
    }

    private static Auction ScheduledAuction() => Auction.Create(
        SellerId,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        100m,
        5m,
        TestHarness.Now.AddHours(1),
        TestHarness.Now.AddHours(2),
        TestHarness.Now);
}
