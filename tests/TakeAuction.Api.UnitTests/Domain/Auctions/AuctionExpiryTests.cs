using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionExpiryTests
{
    private static readonly Guid SellerId = Guid.CreateVersion7();

    [Fact]
    public void Closes_an_auction_whose_window_has_elapsed()
    {
        var auction = ActiveAuction();

        var ended = auction.End(auction.EndsAtUtc);

        Assert.True(ended);
        Assert.Equal(AuctionStatus.Ended, auction.Status);
    }

    [Fact]
    public void Leaves_an_auction_that_is_still_running_untouched()
    {
        var auction = ActiveAuction();

        var ended = auction.End(auction.EndsAtUtc.AddSeconds(-1));

        Assert.False(ended);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void Closing_an_already_closed_auction_is_a_no_op()
    {
        var auction = ActiveAuction();
        auction.End(auction.EndsAtUtc);

        var endedAgain = auction.End(auction.EndsAtUtc.AddHours(1));

        Assert.False(endedAgain);
        Assert.Equal(AuctionStatus.Ended, auction.Status);
    }

    [Fact]
    public void Closes_a_scheduled_auction_that_never_started()
    {
        var auction = Auction.Create(
            SellerId,
            "Rare stamp collection",
            "A detailed description of the lot on offer.",
            100m,
            5m,
            TestHarness.Now.AddHours(1),
            TestHarness.Now.AddHours(2),
            TestHarness.Now);

        Assert.Equal(AuctionStatus.Scheduled, auction.Status);

        var ended = auction.End(TestHarness.Now.AddHours(3));

        Assert.True(ended);
        Assert.Equal(AuctionStatus.Ended, auction.Status);
    }

    [Fact]
    public void Keeps_the_leading_bidder_as_the_winner()
    {
        var auction = ActiveAuction();
        var bidder = Guid.CreateVersion7();

        // Unopposed, a ceiling of 150 wins the lot at the asking price — the winner pays what
        // it took to hold it, not what they were prepared to spend.
        auction.PlaceBid(bidder, 150m, TestHarness.Now);
        auction.End(auction.EndsAtUtc);

        Assert.Equal(bidder, auction.LeadingBidderId);
        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public void An_auction_that_drew_no_bids_ends_without_a_winner()
    {
        var auction = ActiveAuction();

        auction.End(auction.EndsAtUtc);

        Assert.Null(auction.LeadingBidderId);
        Assert.Equal(0, auction.BidCount);
        Assert.Equal(auction.StartingPrice, auction.CurrentPrice);
    }

    [Fact]
    public void A_closed_auction_refuses_further_bids()
    {
        var auction = ActiveAuction();
        auction.End(auction.EndsAtUtc);

        var outcome = auction.PlaceBid(Guid.CreateVersion7(), 5_000m, TestHarness.Now);

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
    }

    private static Auction ActiveAuction() => Auction.Create(
        SellerId,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        100m,
        5m,
        TestHarness.Now,
        TestHarness.Now.AddDays(2),
        TestHarness.Now);
}
