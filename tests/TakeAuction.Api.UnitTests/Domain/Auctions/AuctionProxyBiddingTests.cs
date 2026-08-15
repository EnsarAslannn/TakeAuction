using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Domain.Auctions;

public sealed class AuctionProxyBiddingTests
{
    private static readonly Guid Seller = Guid.CreateVersion7();
    private static readonly Guid Ada = Guid.CreateVersion7();
    private static readonly Guid Bruno = Guid.CreateVersion7();
    private static readonly Guid Cem = Guid.CreateVersion7();

    [Fact]
    public void An_unopposed_ceiling_takes_the_lot_at_the_asking_price()
    {
        var auction = OpenAuction();

        var outcome = auction.PlaceBid(Ada, 500m, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(500m, auction.LeadingMaxAmount);
        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Null(outcome.AutomaticBid);
    }

    [Fact]
    public void A_challenger_under_the_ceiling_only_moves_the_price()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Bruno, 300m, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Equal(305m, auction.CurrentPrice);
        Assert.Equal(500m, auction.LeadingMaxAmount);
    }

    [Fact]
    public void The_house_answers_for_the_leader_and_says_so()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Bruno, 300m, TestHarness.Now);

        Assert.NotNull(outcome.AutomaticBid);
        Assert.Equal(Ada, outcome.AutomaticBid.BidderId);
        Assert.Equal(305m, outcome.AutomaticBid.Amount);
        Assert.True(outcome.AutomaticBid.IsAutomatic);

        Assert.Equal(Bruno, outcome.Bid!.BidderId);
        Assert.Equal(300m, outcome.Bid.Amount);
        Assert.False(outcome.Bid.IsAutomatic);

        Assert.Same(outcome.AutomaticBid, outcome.PriceSetter);
    }

    [Fact]
    public void A_challenger_who_cannot_be_answered_in_full_stops_at_the_leader_s_ceiling()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        // Answering 498 by the full increment would take Ada past the 500 she agreed to.
        auction.PlaceBid(Bruno, 498m, TestHarness.Now);

        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Equal(500m, auction.CurrentPrice);
    }

    [Fact]
    public void A_higher_ceiling_takes_the_lot_for_one_increment_over_the_one_it_beat()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Bruno, 900m, TestHarness.Now);

        Assert.Equal(Bruno, auction.LeadingBidderId);
        Assert.Equal(505m, auction.CurrentPrice);
        Assert.Equal(900m, auction.LeadingMaxAmount);
        Assert.Null(outcome.AutomaticBid);
    }

    [Fact]
    public void A_winner_who_only_just_clears_the_ceiling_pays_their_own_maximum()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        auction.PlaceBid(Bruno, 502m, TestHarness.Now);

        Assert.Equal(Bruno, auction.LeadingBidderId);
        Assert.Equal(502m, auction.CurrentPrice);
    }

    [Fact]
    public void Matching_a_ceiling_is_not_beating_it()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        auction.PlaceBid(Bruno, 500m, TestHarness.Now);

        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Equal(500m, auction.CurrentPrice);
    }

    [Fact]
    public void A_ceiling_that_does_not_clear_the_visible_price_is_refused()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Bruno, 104.99m, TestHarness.Now);

        Assert.Equal(BidRejection.BidTooLow, outcome.Rejection);
        Assert.Equal(100m, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
    }

    [Fact]
    public void A_leader_raises_their_own_ceiling_without_moving_the_price()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);
        auction.PlaceBid(Bruno, 300m, TestHarness.Now);

        var outcome = auction.PlaceBid(Ada, 900m, TestHarness.Now);

        Assert.True(outcome.Succeeded);
        Assert.Equal(305m, auction.CurrentPrice);
        Assert.Equal(900m, auction.LeadingMaxAmount);
        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Null(outcome.AutomaticBid);
    }

    [Fact]
    public void A_leader_cannot_lower_their_own_ceiling()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Ada, 400m, TestHarness.Now);

        Assert.Equal(BidRejection.BidTooLow, outcome.Rejection);
        Assert.Equal(500m, auction.LeadingMaxAmount);
    }

    [Fact]
    public void A_leader_must_clear_their_own_ceiling_by_the_increment()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        Assert.Equal(505m, auction.MinimumAcceptableBidFor(Ada));
        Assert.Equal(105m, auction.MinimumAcceptableBidFor(Bruno));

        Assert.Equal(BidRejection.BidTooLow, auction.PlaceBid(Ada, 504.99m, TestHarness.Now).Rejection);
        Assert.True(auction.PlaceBid(Ada, 505m, TestHarness.Now).Succeeded);
    }

    [Fact]
    public void A_raised_ceiling_defends_the_lot_against_the_next_challenger()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);
        auction.PlaceBid(Ada, 900m, TestHarness.Now);

        auction.PlaceBid(Bruno, 700m, TestHarness.Now);

        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.Equal(705m, auction.CurrentPrice);
    }

    [Fact]
    public void Three_bidders_settle_at_one_increment_over_the_runner_up()
    {
        var auction = OpenAuction();

        auction.PlaceBid(Ada, 200m, TestHarness.Now);
        auction.PlaceBid(Bruno, 600m, TestHarness.Now);
        auction.PlaceBid(Cem, 450m, TestHarness.Now);

        Assert.Equal(Bruno, auction.LeadingBidderId);
        Assert.Equal(455m, auction.CurrentPrice);
    }

    [Fact]
    public void A_lot_never_settles_above_the_winning_ceiling()
    {
        var auction = OpenAuction();

        auction.PlaceBid(Ada, 1000m, TestHarness.Now);
        auction.PlaceBid(Bruno, 999m, TestHarness.Now);

        Assert.Equal(Ada, auction.LeadingBidderId);
        Assert.True(auction.CurrentPrice <= auction.LeadingMaxAmount);
        Assert.Equal(1000m, auction.CurrentPrice);
    }

    [Fact]
    public void Every_bid_the_house_placed_is_recorded_against_the_bidder_it_defended()
    {
        var auction = OpenAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);

        var outcome = auction.PlaceBid(Bruno, 300m, TestHarness.Now);

        Assert.Equal(3, auction.BidCount);
        Assert.Equal(auction.Id, outcome.AutomaticBid!.AuctionId);
        Assert.Equal(500m, outcome.AutomaticBid.MaxAmount);
        Assert.Null(outcome.AutomaticBid.IdempotencyKey);
    }

    [Fact]
    public void Raising_a_ceiling_buys_no_extra_time_on_the_clock()
    {
        var auction = ClosingAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);
        var endBeforeTheNudge = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Ada, 900m, endBeforeTheNudge.AddSeconds(-1));

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.Extended);
        Assert.Equal(endBeforeTheNudge, auction.EndsAtUtc);
    }

    [Fact]
    public void A_challenger_answered_by_a_proxy_still_buys_everyone_extra_time()
    {
        var auction = ClosingAuction();
        auction.PlaceBid(Ada, 500m, TestHarness.Now);
        var endBeforeTheSnipe = auction.EndsAtUtc;

        var outcome = auction.PlaceBid(Bruno, 300m, endBeforeTheSnipe.AddSeconds(-1));

        Assert.True(outcome.Extended);
        Assert.True(auction.EndsAtUtc > endBeforeTheSnipe);
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

    private static Auction ClosingAuction() => Auction.Create(
        Seller,
        "Rare stamp collection",
        "A detailed description of the lot on offer.",
        100m,
        5m,
        TestHarness.Now,
        TestHarness.Now.AddHours(1),
        TestHarness.Now);
}
