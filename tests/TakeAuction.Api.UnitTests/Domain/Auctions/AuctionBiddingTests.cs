using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionBiddingTests
{
    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Guid Bidder = Guid.CreateVersion7();
    private static readonly Guid OtherBidder = Guid.CreateVersion7();

    [Fact]
    public void First_bid_may_equal_the_starting_price()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 100m, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(Bidder, auction.LeadingBidderId);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public void First_bid_below_the_starting_price_is_rejected()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 99.99m, TestHarness.Now);

        Assert.Equal(BidRejection.BidTooLow, outcome.Rejection);
        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(0, auction.BidCount);
        Assert.Null(auction.LeadingBidderId);
    }

    [Fact]
    public void Subsequent_bid_must_clear_the_minimum_increment()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Bidder, 100m, TestHarness.Now);

        var tooLow = auction.PlaceBid(OtherBidder, 104.99m, TestHarness.Now);
        var justEnough = auction.PlaceBid(OtherBidder, 105m, TestHarness.Now);

        Assert.Equal(BidRejection.BidTooLow, tooLow.Rejection);
        Assert.True(justEnough.Succeeded);
        Assert.Equal(105m, auction.CurrentPrice);
        Assert.Equal(OtherBidder, auction.LeadingBidderId);
        Assert.Equal(2, auction.BidCount);
    }

    [Fact]
    public void Minimum_acceptable_bid_tracks_the_current_price()
    {
        var auction = OpenAuction();

        Assert.Equal(100m, auction.MinimumAcceptableBid);

        // The opener's ceiling of 120 buys the lot at the asking price, so the next rival is
        // bidding against 100, not against a number only the house can see.
        auction.PlaceBid(Bidder, 120m, TestHarness.Now);

        Assert.Equal(105m, auction.MinimumAcceptableBid);
    }

    [Fact]
    public void Seller_cannot_bid_on_their_own_auction()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Seller, 500m, TestHarness.Now);

        Assert.Equal(BidRejection.SellerCannotBid, outcome.Rejection);
        Assert.Equal(0, auction.BidCount);
    }

    [Fact]
    public void Bid_before_the_start_time_is_rejected()
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

        var outcome = auction.PlaceBid(Bidder, 200m, TestHarness.Now);

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
        Assert.Equal(AuctionStatus.Scheduled, auction.Status);
    }

    [Fact]
    public void Bid_after_the_end_time_is_rejected()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 200m, TestHarness.Now.AddDays(3));

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
        Assert.Equal(0, auction.BidCount);
    }

    [Fact]
    public void Bid_exactly_at_the_end_time_is_rejected()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 200m, auction.EndsAtUtc);

        Assert.Equal(BidRejection.AuctionNotOpen, outcome.Rejection);
    }

    [Fact]
    public void A_scheduled_auction_becomes_active_once_its_first_bid_lands()
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

        Assert.Equal(AuctionStatus.Scheduled, auction.Status);

        var outcome = auction.PlaceBid(Bidder, 100m, TestHarness.Now.AddHours(3));

        Assert.True(outcome.Succeeded);
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }

    [Fact]
    public void The_returned_bid_carries_the_auction_and_bidder()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 150m, TestHarness.Now);

        Assert.NotNull(outcome.Bid);
        Assert.Equal(auction.Id, outcome.Bid.AuctionId);
        Assert.Equal(Bidder, outcome.Bid.BidderId);
        Assert.Equal(100m, outcome.Bid.Amount);
        Assert.Equal(150m, outcome.Bid.MaxAmount);
        Assert.False(outcome.Bid.IsAutomatic);
        Assert.Equal(TestHarness.Now, outcome.Bid.PlacedAtUtc);
    }

    [Fact]
    public void A_rejected_bid_produces_no_bid_record()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Bidder, 1m, TestHarness.Now);

        Assert.Null(outcome.Bid);
    }

    [Fact]
    public void An_empty_bidder_id_is_a_programming_error()
    {
        var auction = OpenAuction();

        Assert.Throws<ArgumentException>(() => auction.PlaceBid(Guid.Empty, 200m, TestHarness.Now));
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
