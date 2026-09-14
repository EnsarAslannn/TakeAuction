using System.Net;
using System.Net.Http.Json;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.GetBidStanding;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetBidStandingTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private readonly IntegrationTestFixture _fixture;

    private Guid _auctionId;

    public GetBidStandingTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _auctionId = await CreateAuctionAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_anonymous_callers()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(StandingUrl(_auctionId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reports_an_unknown_auction()
    {
        var client = await CreateBidderClientAsync();

        var response = await client.GetAsync(StandingUrl(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A_bidder_on_an_untouched_lot_has_to_meet_the_starting_price()
    {
        var client = await CreateBidderClientAsync();

        var standing = await ReadStandingAsync(client);

        Assert.False(standing.IsLeading);
        Assert.Null(standing.MaxAmount);
        Assert.Equal(StartingPrice, standing.MinimumAcceptableBid);
    }

    [Fact]
    public async Task The_leader_has_to_clear_their_own_ceiling_rather_than_the_price()
    {
        var leader = await CreateBidderClientAsync();
        await BidAsync(leader, 500m);

        var standing = await ReadStandingAsync(leader);

        Assert.True(standing.IsLeading);
        Assert.Equal(500m, standing.MaxAmount);
        Assert.Equal(505m, standing.MinimumAcceptableBid);
    }

    [Fact]
    public async Task The_floor_it_reports_is_the_one_the_bid_endpoint_enforces()
    {
        var leader = await CreateBidderClientAsync();
        await BidAsync(leader, 500m);

        var floor = (await ReadStandingAsync(leader)).MinimumAcceptableBid;

        var under = await leader.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(floor - 0.01m));
        Assert.Equal(HttpStatusCode.BadRequest, under.StatusCode);

        var at = await leader.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(floor));
        Assert.Equal(HttpStatusCode.OK, at.StatusCode);
    }

    [Fact]
    public async Task A_raised_ceiling_shows_up_straight_away()
    {
        var leader = await CreateBidderClientAsync();
        await BidAsync(leader, 500m);
        await ReadStandingAsync(leader);

        await BidAsync(leader, 700m);

        var standing = await ReadStandingAsync(leader);

        Assert.Equal(700m, standing.MaxAmount);
        Assert.Equal(705m, standing.MinimumAcceptableBid);
    }

    [Fact]
    public async Task A_beaten_challenger_is_not_leading_and_learns_nothing_of_the_leader_s_ceiling()
    {
        var leader = await CreateBidderClientAsync();
        var challenger = await CreateBidderClientAsync();

        await BidAsync(leader, 500m);
        await BidAsync(challenger, 300m);

        var standing = await ReadStandingAsync(challenger);

        Assert.False(standing.IsLeading);
        Assert.Null(standing.MaxAmount);
        Assert.Equal(310m, standing.MinimumAcceptableBid);
    }

    [Fact]
    public async Task A_leader_who_is_overtaken_drops_back_to_the_public_floor()
    {
        var first = await CreateBidderClientAsync();
        var second = await CreateBidderClientAsync();

        await BidAsync(first, 200m);
        await BidAsync(second, 400m);

        var standing = await ReadStandingAsync(first);

        Assert.False(standing.IsLeading);
        Assert.Null(standing.MaxAmount);
        Assert.Equal(210m, standing.MinimumAcceptableBid);
    }

    private async Task<BidStandingResponse> ReadStandingAsync(HttpClient client)
    {
        var standing = await client.GetFromJsonAsync<BidStandingResponse>(
            StandingUrl(_auctionId),
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(standing);
        Assert.Equal(_auctionId, standing.AuctionId);

        return standing;
    }

    private async Task BidAsync(HttpClient client, decimal amount) =>
        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(amount))).EnsureSuccessStatusCode();

    private static string StandingUrl(Guid auctionId) => $"/api/v1/auctions/{auctionId}/standing";

    private static string BidsUrl(Guid auctionId) => $"/api/v1/auctions/{auctionId}/bids";

    private async Task<HttpClient> CreateBidderClientAsync()
    {
        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);

        return await _fixture.CreateClientAsAsync(bidder);
    }

    private async Task<Guid> CreateAuctionAsync()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);

        return await _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var auction = Auction.Create(
                seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                now,
                now.AddDays(2),
                now);

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });
    }
}
