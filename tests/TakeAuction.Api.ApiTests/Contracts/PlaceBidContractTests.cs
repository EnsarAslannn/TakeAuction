using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class PlaceBidContractTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public PlaceBidContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Bidding Seller");
        _auctionId = await _fixture.CreateOpenAuctionAsync(_seller, StartingPrice, Increment);
    }

    public Task DisposeAsync()
    {
        _seller.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonymous_callers_are_challenged()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_auction_is_a_problem_document()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        var missingId = Guid.CreateVersion7();

        var response = await bidder.PostAsync(ApiRoutes.Bids(missingId), new { amount = 150m });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await bidder.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);
    }

    [Fact]
    public async Task A_seller_may_not_bid_on_their_own_lot()
    {
        var response = await _seller.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await _seller.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Sellers cannot bid on their own auction", problem.Title);
    }

    [Fact]
    public async Task A_bid_under_the_starting_price_is_refused_with_the_floor_in_the_detail()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 99m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await bidder.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Bid is too low", problem.Title);
        Assert.Contains("100.00", problem.Detail);
    }

    [Fact]
    public async Task A_bid_that_does_not_clear_the_increment_is_refused()
    {
        using var first = await _fixture.CreateBidderAsync();
        (await first.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        using var second = await _fixture.CreateBidderAsync();
        var response = await second.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 104m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await second.ReadAsync<ProblemDetails>(response);

        Assert.Contains("105.00", problem.Detail);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task A_non_positive_amount_never_reaches_the_auction(decimal amount)
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await bidder.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(PlaceBidCommand.Amount), problem.Errors.Keys);
    }

    [Fact]
    public async Task An_amount_with_too_many_decimals_is_a_validation_failure()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150.12345m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await bidder.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(PlaceBidCommand.Amount), problem.Errors.Keys);
    }

    [Fact]
    public async Task An_auction_that_has_not_opened_yet_reports_a_conflict()
    {
        var createResponse = await _seller.PostAsync(ApiRoutes.Auctions, ApiTestFixture.ScheduledAuctionRequest());
        var scheduled = await _seller.ReadAsync<TakeAuction.Api.Features.Auctions.CreateAuction.CreateAuctionResponse>(
            createResponse);

        using var bidder = await _fixture.CreateBidderAsync();
        var response = await bidder.PostAsync(ApiRoutes.Bids(scheduled.Id), new { amount = 150m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await bidder.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction is not open for bidding", problem.Title);
    }

    [Fact]
    public async Task An_accepted_bid_returns_the_receipt_the_client_renders()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var receipt = await bidder.ReadAsync<PlaceBidResponse>(response);

        Assert.NotEqual(Guid.Empty, receipt.BidId);
        Assert.Equal(_auctionId, receipt.AuctionId);
        Assert.Equal(StartingPrice, receipt.Amount);
        Assert.Equal(150m, receipt.MaxAmount);
        Assert.Equal(StartingPrice, receipt.CurrentPrice);
        Assert.True(receipt.IsLeading);
        Assert.False(receipt.AnsweredByProxy);
        Assert.Equal(155m, receipt.MinimumNextBid);
        Assert.Equal(1, receipt.BidCount);
        Assert.True(receipt.PlacedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task An_accepted_bid_moves_the_price_the_next_reader_sees()
    {
        using var leader = await _fixture.CreateBidderAsync();
        using var challenger = await _fixture.CreateBidderAsync();

        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 500m })).EnsureSuccessStatusCode();
        (await challenger.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 300m })).EnsureSuccessStatusCode();

        using var reader = _fixture.CreateSession();
        var detail = await reader.ReadAsync<AuctionDetailResponse>(
            await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

        Assert.Equal(305m, detail.CurrentPrice);
        Assert.Equal(310m, detail.MinimumAcceptableBid);
        Assert.Equal(3, detail.BidCount);
    }

    [Fact]
    public async Task A_challenger_under_the_leader_s_ceiling_is_told_it_did_not_take_the_lot()
    {
        using var leader = await _fixture.CreateBidderAsync();
        using var challenger = await _fixture.CreateBidderAsync();

        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 500m })).EnsureSuccessStatusCode();

        var response = await challenger.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 300m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var receipt = await challenger.ReadAsync<PlaceBidResponse>(response);

        Assert.False(receipt.IsLeading);
        Assert.True(receipt.AnsweredByProxy);
        Assert.Equal(300m, receipt.Amount);
        Assert.Equal(305m, receipt.CurrentPrice);
        Assert.Equal(310m, receipt.MinimumNextBid);
    }

    [Fact]
    public async Task A_leader_is_told_what_they_have_to_clear_to_raise_their_own_ceiling()
    {
        using var leader = await _fixture.CreateBidderAsync();

        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 500m })).EnsureSuccessStatusCode();

        var response = await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 400m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await leader.ReadAsync<ProblemDetails>(response);

        Assert.Contains("505.00", problem.Detail);
    }

    [Fact]
    public async Task A_ladder_of_bids_leaves_the_top_bid_standing()
    {
        var bidders = await _fixture.CreateBiddersAsync(3);

        try
        {
            for (var index = 0; index < bidders.Count; index++)
            {
                var amount = 150m + (index * Increment);
                var response = await bidders[index].PostAsync(ApiRoutes.Bids(_auctionId), new { amount });

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using var reader = _fixture.CreateSession();
            var detail = await reader.ReadAsync<AuctionDetailResponse>(
                await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

            Assert.Equal(160m, detail.CurrentPrice);
            Assert.Equal(3, detail.BidCount);
        }
        finally
        {
            foreach (var bidder in bidders)
            {
                bidder.Dispose();
            }
        }
    }

    [Fact]
    public async Task Only_one_of_many_simultaneous_identical_bids_is_accepted()
    {
        const int bidderCount = 15;
        var bidders = await _fixture.CreateBiddersAsync(bidderCount);

        try
        {
            var responses = await Task.WhenAll(
                bidders.Select(bidder => bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = StartingPrice })));

            var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

            Assert.Equal(1, accepted);
            Assert.All(
                responses.Where(response => response.StatusCode != HttpStatusCode.OK),
                response => Assert.Contains(
                    response.StatusCode,
                    new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));

            using var reader = _fixture.CreateSession();
            var detail = await reader.ReadAsync<AuctionDetailResponse>(
                await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

            Assert.Equal(StartingPrice, detail.CurrentPrice);
            Assert.Equal(1, detail.BidCount);
        }
        finally
        {
            foreach (var bidder in bidders)
            {
                bidder.Dispose();
            }
        }
    }

    [Fact]
    public async Task Simultaneous_ascending_bids_all_settle_on_one_consistent_price()
    {
        const int bidderCount = 10;
        var bidders = await _fixture.CreateBiddersAsync(bidderCount);

        try
        {
            var responses = await Task.WhenAll(
                bidders.Select((bidder, index) =>
                    bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = StartingPrice + (index * Increment) })));

            var acceptedAmounts = new List<decimal>();

            foreach (var response in responses.Where(response => response.StatusCode == HttpStatusCode.OK))
            {
                var receipt = await response.Content.ReadFromJsonAsync<PlaceBidResponse>(ApiTestFixture.JsonOptions);
                acceptedAmounts.Add(receipt!.Amount);
            }

            Assert.NotEmpty(acceptedAmounts);

            using var reader = _fixture.CreateSession();
            var detail = await reader.ReadAsync<AuctionDetailResponse>(
                await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

            Assert.True(
                detail.CurrentPrice >= acceptedAmounts.Max(),
                $"the lot shows {detail.CurrentPrice}, below an accepted bid of {acceptedAmounts.Max()}");

            Assert.True(detail.BidCount >= acceptedAmounts.Count);
            Assert.Equal(detail.CurrentPrice + Increment, detail.MinimumAcceptableBid);
        }
        finally
        {
            foreach (var bidder in bidders)
            {
                bidder.Dispose();
            }
        }
    }
}
