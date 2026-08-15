using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionCancellationTests
{
    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Guid Bidder = Guid.CreateVersion7();

    [Fact]
    public void A_seller_may_withdraw_a_lot_nobody_has_bid_on()
    {
        var auction = OpenAuction();

        var outcome = auction.Cancel(Seller, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
    }

    [Fact]
    public void A_lot_that_has_drawn_a_bid_can_no_longer_be_withdrawn()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Bidder, 150m, TestHarness.Now);

        var outcome = auction.Cancel(Seller, TestHarness.Now);

        Assert.Equal(CancelRejection.AlreadyBidOn, outcome.Rejection);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Nobody_but_the_seller_may_withdraw_a_lot()
    {
        var auction = OpenAuction();

        var outcome = auction.Cancel(Bidder, TestHarness.Now);

        Assert.Equal(CancelRejection.NotTheSeller, outcome.Rejection);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void A_lot_already_past_its_close_cannot_be_withdrawn()
    {
        var auction = OpenAuction();

        var outcome = auction.Cancel(Seller, auction.EndsAtUtc);

        Assert.Equal(CancelRejection.AlreadyClosed, outcome.Rejection);
    }

    [Fact]
    public void A_lot_the_sweep_has_already_closed_cannot_be_withdrawn()
    {
        var auction = OpenAuction();
        auction.End(auction.EndsAtUtc);

        var outcome = auction.Cancel(Seller, TestHarness.Now);

        Assert.Equal(CancelRejection.AlreadyClosed, outcome.Rejection);
        Assert.Equal(AuctionStatus.Ended, auction.Status);
    }

    [Fact]
    public void Withdrawing_twice_is_refused_the_second_time()
    {
        var auction = OpenAuction();
        auction.Cancel(Seller, TestHarness.Now);

        var outcome = auction.Cancel(Seller, TestHarness.Now);

        Assert.Equal(CancelRejection.AlreadyCancelled, outcome.Rejection);
    }

    [Fact]
    public void A_scheduled_lot_can_be_withdrawn_before_it_opens()
    {
        var auction = Auction.Create(
            Seller,
            "Antique oak desk",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now.AddHours(2),
            TestHarness.Now.AddDays(2),
            TestHarness.Now);

        var outcome = auction.Cancel(Seller, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
    }

    [Fact]
    public void A_withdrawn_lot_takes_no_further_bids()
    {
        var auction = OpenAuction();
        auction.Cancel(Seller, TestHarness.Now);

        var outcome = auction.PlaceBid(Bidder, 150m, TestHarness.Now);

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
    }

    [Fact]
    public void A_withdrawn_lot_is_not_closed_again_by_the_sweep()
    {
        var auction = OpenAuction();
        auction.Cancel(Seller, TestHarness.Now);

        Assert.False(auction.End(auction.EndsAtUtc));
        Assert.Equal(AuctionStatus.Cancelled, auction.Status);
    }

    [Fact]
    public void An_empty_seller_id_is_a_programming_error()
    {
        var auction = OpenAuction();

        Assert.Throws<ArgumentException>(() => auction.Cancel(Guid.Empty, TestHarness.Now));
    }

    private static Auction OpenAuction() => Auction.Create(
        Seller,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        100m,
        5m,
        TestHarness.Now,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
