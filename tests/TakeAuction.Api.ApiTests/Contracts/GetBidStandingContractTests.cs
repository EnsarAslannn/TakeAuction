using System.Net;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auctions.GetBidStanding;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class GetBidStandingContractTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 10m;
    private const decimal LeaderCeiling = 400m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public GetBidStandingContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync();
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

        var response = await client.GetAsync(ApiRoutes.Standing(_auctionId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_auction_is_a_problem_document()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.GetAsync(ApiRoutes.Standing(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await bidder.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);
    }

    [Fact]
    public async Task The_leader_reads_the_shape_the_client_renders()
    {
        using var leader = await _fixture.CreateBidderAsync();
        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = LeaderCeiling })).EnsureSuccessStatusCode();

        var response = await leader.GetAsync(ApiRoutes.Standing(_auctionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonAssert.Root(await response.Content.ReadAsStringAsync());

        JsonAssert.HasProperties(body, "auctionId", "isLeading", "maxAmount", "minimumAcceptableBid");
        Assert.True(body.GetProperty("isLeading").GetBoolean());
        Assert.Equal(LeaderCeiling, body.GetProperty("maxAmount").GetDecimal());
        Assert.Equal(LeaderCeiling + Increment, body.GetProperty("minimumAcceptableBid").GetDecimal());
    }

    [Fact]
    public async Task Nobody_but_the_leader_ever_reads_the_leader_s_ceiling()
    {
        using var leader = await _fixture.CreateBidderAsync();
        using var rival = await _fixture.CreateBidderAsync();

        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = LeaderCeiling })).EnsureSuccessStatusCode();

        var response = await rival.GetAsync(ApiRoutes.Standing(_auctionId));

        var standing = await rival.ReadAsync<BidStandingResponse>(response);

        Assert.False(standing.IsLeading);
        Assert.Null(standing.MaxAmount);
        Assert.Equal(StartingPrice + Increment, standing.MinimumAcceptableBid);
    }
}
