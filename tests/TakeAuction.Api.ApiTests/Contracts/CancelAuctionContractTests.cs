using System.Net;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.CancelAuction;
using TakeAuction.Api.Features.Auctions.GetAuctionById;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class CancelAuctionContractTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public CancelAuctionContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Withdrawing Seller");
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

        var response = await client.PostAsync(ApiRoutes.Cancel(_auctionId), null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_bidder_may_not_reach_the_endpoint_at_all()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Cancel(_auctionId), new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_seller_withdraws_a_lot_nobody_has_bid_on()
    {
        var response = await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await _seller.ReadAsync<CancelAuctionResponse>(response);

        Assert.Equal(_auctionId, body.Id);
        Assert.Equal(nameof(AuctionStatus.Cancelled), body.Status);
    }

    [Fact]
    public async Task The_next_reader_sees_the_lot_withdrawn()
    {
        (await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { })).EnsureSuccessStatusCode();

        using var reader = _fixture.CreateSession();
        var detail = await reader.ReadAsync<AuctionDetailResponse>(
            await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

        Assert.Equal(nameof(AuctionStatus.Cancelled), detail.Status);
    }

    [Fact]
    public async Task A_withdrawn_lot_takes_no_further_bids()
    {
        (await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { })).EnsureSuccessStatusCode();

        using var bidder = await _fixture.CreateBidderAsync();
        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_lot_that_has_been_bid_on_cannot_be_withdrawn()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        var response = await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await _seller.ReadAsync<ProblemDetails>(response);

        Assert.Equal("The lot has already been bid on", problem.Title);
    }

    [Fact]
    public async Task Withdrawing_twice_is_refused_the_second_time()
    {
        (await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { })).EnsureSuccessStatusCode();

        var response = await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await _seller.ReadAsync<ProblemDetails>(response);

        Assert.Equal("The lot has already been withdrawn", problem.Title);
    }

    [Fact]
    public async Task An_unknown_auction_is_a_problem_document()
    {
        var response = await _seller.PostAsync(ApiRoutes.Cancel(Guid.CreateVersion7()), new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await _seller.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);
    }

    [Fact]
    public async Task Another_seller_is_told_the_lot_does_not_exist_rather_than_that_it_is_not_theirs()
    {
        using var stranger = await _fixture.CreateSellerAsync("Passing Seller");

        var response = await stranger.PostAsync(ApiRoutes.Cancel(_auctionId), new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await stranger.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);

        using var reader = _fixture.CreateSession();
        var detail = await reader.ReadAsync<AuctionDetailResponse>(
            await reader.GetAsync(ApiRoutes.Auction(_auctionId)));

        Assert.Equal(nameof(AuctionStatus.Active), detail.Status);
    }
}
