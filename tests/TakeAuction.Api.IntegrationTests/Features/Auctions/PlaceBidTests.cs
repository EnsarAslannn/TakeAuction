using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Messaging.Contracts;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Auctions.PlaceBid;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class PlaceBidTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private readonly IntegrationTestFixture _fixture;

    private User _seller = null!;
    private Guid _auctionId;

    public PlaceBidTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateUserAsync(UserRole.Seller);
        _auctionId = await CreateAuctionAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_anonymous_callers()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reports_an_unknown_auction()
    {
        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(Guid.CreateVersion7()), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_the_seller_bidding_on_their_own_auction()
    {
        var client = await _fixture.CreateClientAsAsync(_seller);

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_a_bid_below_the_starting_price()
    {
        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(99m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _fixture.ExecuteDbContextAsync(db => db.Bids.ToListAsync()));
    }

    [Fact]
    public async Task Rejects_a_bid_that_does_not_clear_the_increment()
    {
        var first = await CreateBidderClientAsync();
        (await first.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m))).EnsureSuccessStatusCode();

        var second = await CreateBidderClientAsync();
        var response = await second.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(154m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(await _fixture.ExecuteDbContextAsync(db => db.Bids.ToListAsync()));
    }

    [Fact]
    public async Task Rejects_a_malformed_amount_before_reaching_the_auction()
    {
        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(-1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_a_bid_on_an_auction_that_has_not_started()
    {
        var scheduledId = await CreateAuctionAsync(startsIn: TimeSpan.FromHours(2));
        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(scheduledId), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_a_bid_on_an_auction_that_has_ended()
    {
        var endedId = await CreateAuctionAsync(startsIn: TimeSpan.FromHours(-4), runsFor: TimeSpan.FromHours(2));
        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(endedId), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Accepts_a_valid_bid_and_advances_the_auction()
    {
        var versionBefore = await _fixture.ExecuteDbContextAsync(db =>
            db.Auctions.Where(a => a.Id == _auctionId).Select(a => a.Version).SingleAsync());

        var client = await CreateBidderClientAsync();

        var response = await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaceBidResponse>(IntegrationTestFixture.JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(150m, body.Amount);
        Assert.Equal(150m, body.CurrentPrice);
        Assert.Equal(155m, body.MinimumNextBid);
        Assert.Equal(1, body.BidCount);

        var auction = await _fixture.ExecuteDbContextAsync(db =>
            db.Auctions.SingleAsync(a => a.Id == _auctionId));
        var bid = await _fixture.ExecuteDbContextAsync(db => db.Bids.SingleAsync());

        Assert.Equal(150m, auction.CurrentPrice);
        Assert.Equal(1, auction.BidCount);
        Assert.Equal(bid.BidderId, auction.LeadingBidderId);
        Assert.NotEqual(versionBefore, auction.Version);
    }

    [Fact]
    public async Task Refreshes_the_cached_auction_detail()
    {
        var client = await CreateBidderClientAsync();

        var beforeBid = await client.GetFromJsonAsync<AuctionDetailResponse>(
            $"/api/v1/auctions/{_auctionId}",
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(beforeBid);
        Assert.Equal(StartingPrice, beforeBid.CurrentPrice);

        (await client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(150m))).EnsureSuccessStatusCode();

        var afterBid = await client.GetFromJsonAsync<AuctionDetailResponse>(
            $"/api/v1/auctions/{_auctionId}",
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(afterBid);
        Assert.Equal(150m, afterBid.CurrentPrice);
    }

    [Fact]
    public async Task Only_one_of_many_identical_simultaneous_bids_is_accepted()
    {
        const int bidders = 20;
        var clients = await CreateBidderClientsAsync(bidders);

        var responses = await Task.WhenAll(
            clients.Select(client => client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(StartingPrice))));

        var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, accepted);

        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Contains(
                response.StatusCode,
                new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict }));

        var bids = await _fixture.ExecuteDbContextAsync(db => db.Bids.ToListAsync());
        var auction = await _fixture.ExecuteDbContextAsync(db => db.Auctions.SingleAsync(a => a.Id == _auctionId));

        Assert.Single(bids);
        Assert.Equal(1, auction.BidCount);
        Assert.Equal(StartingPrice, auction.CurrentPrice);
        Assert.Equal(bids[0].BidderId, auction.LeadingBidderId);
    }

    [Fact]
    public async Task Concurrent_ascending_bids_never_lose_an_update()
    {
        const int bidders = 12;
        var clients = await CreateBidderClientsAsync(bidders);

        var amounts = Enumerable
            .Range(0, bidders)
            .Select(index => StartingPrice + (index * Increment))
            .ToArray();

        var responses = await Task.WhenAll(
            clients.Select((client, index) =>
                client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(amounts[index]))));

        var acceptedCount = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        Assert.True(acceptedCount >= 1, "at least one concurrent bid must be accepted");

        var bids = await _fixture.ExecuteDbContextAsync(db =>
            db.Bids.OrderBy(bid => bid.Amount).ToListAsync());
        var auction = await _fixture.ExecuteDbContextAsync(db =>
            db.Auctions.SingleAsync(a => a.Id == _auctionId));

        Assert.Equal(acceptedCount, bids.Count);
        Assert.Equal(acceptedCount, auction.BidCount);
        Assert.Equal(bids.Max(bid => bid.Amount), auction.CurrentPrice);
        Assert.Equal(bids.Count, bids.Select(bid => bid.BidderId).Distinct().Count());
        Assert.True(bids[0].Amount >= StartingPrice);

        for (var index = 1; index < bids.Count; index++)
        {
            Assert.True(
                bids[index].Amount >= bids[index - 1].Amount + Increment,
                $"bid {bids[index].Amount} did not clear the increment over {bids[index - 1].Amount}");
        }
    }

    [Fact]
    public async Task Every_accepted_bid_leaves_exactly_one_queued_event_and_every_rejected_one_leaves_none()
    {
        const int bidders = 12;
        var clients = await CreateBidderClientsAsync(bidders);

        var amounts = Enumerable
            .Range(0, bidders)
            .Select(index => StartingPrice + (index * Increment))
            .ToArray();

        var responses = await Task.WhenAll(
            clients.Select((client, index) =>
                client.PostAsJsonAsync(BidsUrl(_auctionId), new PlaceBidRequest(amounts[index]))));

        var accepted = responses.Count(response => response.StatusCode == HttpStatusCode.OK);

        var bidIds = await _fixture.ExecuteDbContextAsync(db =>
            db.Bids.Select(bid => bid.Id).ToListAsync());

        var queued = await _fixture.ExecuteDbContextAsync(db => db.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Type == nameof(BidPlacedIntegrationEvent))
            .ToListAsync());

        Assert.Equal(accepted, queued.Count);

        var announced = queued
            .Select(message => JsonSerializer.Deserialize<BidPlacedIntegrationEvent>(
                message.Payload,
                Outbox.SerializerOptions)!.BidId)
            .ToList();

        Assert.Equal(bidIds.Order(), announced.Order());
    }

    private static string BidsUrl(Guid auctionId) => $"/api/v1/auctions/{auctionId}/bids";

    private async Task<HttpClient> CreateBidderClientAsync()
    {
        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);

        return await _fixture.CreateClientAsAsync(bidder);
    }

    private async Task<IReadOnlyList<HttpClient>> CreateBidderClientsAsync(int count)
    {
        var clients = new List<HttpClient>(count);

        for (var index = 0; index < count; index++)
        {
            clients.Add(await CreateBidderClientAsync());
        }

        return clients;
    }

    private Task<Guid> CreateAuctionAsync(TimeSpan? startsIn = null, TimeSpan? runsFor = null) =>
        _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var startsAt = now.Add(startsIn ?? TimeSpan.Zero);

            var auction = Auction.Create(
                _seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                StartingPrice,
                Increment,
                startsAt,
                startsAt.Add(runsFor ?? TimeSpan.FromDays(2)),
                now);

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });
}
