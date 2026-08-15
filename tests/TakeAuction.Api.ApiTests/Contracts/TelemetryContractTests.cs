using System.Net;
using TakeAuction.Api.ApiTests.Common;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class TelemetryContractTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 5m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public TelemetryContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Telemetry Seller");
        _auctionId = await _fixture.CreateOpenAuctionAsync(_seller, StartingPrice, Increment);
    }

    public Task DisposeAsync()
    {
        _seller.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_scrape_endpoint_is_open_to_the_collector()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_bid_shows_up_on_the_scrape()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        using var client = _fixture.CreateRawClient();
        var scrape = await client.GetStringAsync("/metrics");

        Assert.Contains("takeauction_bids", scrape, StringComparison.Ordinal);
        Assert.Contains("outcome=\"accepted\"", scrape, StringComparison.Ordinal);
        Assert.Contains("takeauction_bids_duration", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refused_bid_is_told_apart_from_an_accepted_one_on_the_scrape()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 1m });

        using var client = _fixture.CreateRawClient();
        var scrape = await client.GetStringAsync("/metrics");

        Assert.Contains("outcome=\"BidTooLow\"", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_runtime_and_request_instruments_come_along_for_free()
    {
        using var client = _fixture.CreateRawClient();
        await client.GetAsync(ApiRoutes.Auctions);

        var scrape = await client.GetStringAsync("/metrics");

        Assert.Contains("http_server_request_duration", scrape, StringComparison.Ordinal);
        Assert.Contains("dotnet_gc", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_scrape_does_not_leak_a_bidder_s_sealed_ceiling()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 987654m })).EnsureSuccessStatusCode();

        using var client = _fixture.CreateRawClient();
        var scrape = await client.GetStringAsync("/metrics");

        Assert.DoesNotContain("987654", scrape, StringComparison.Ordinal);
    }
}
