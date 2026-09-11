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

        var scrape = await ScrapeContainingAsync("outcome=\"accepted\"");

        Assert.Contains("takeauction_bids", scrape, StringComparison.Ordinal);
        Assert.Contains("outcome=\"accepted\"", scrape, StringComparison.Ordinal);
        Assert.Contains("takeauction_bids_duration", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retry_passes_are_bucketed_one_pass_at_a_time()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        var scrape = await ScrapeContainingAsync("takeauction_bids_attempts_bucket");

        var buckets = scrape
            .Split('\n')
            .Where(line => line.StartsWith("takeauction_bids_attempts_bucket", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(buckets, line => line.Contains("le=\"1\"", StringComparison.Ordinal));
        Assert.Contains(buckets, line => line.Contains("le=\"2\"", StringComparison.Ordinal));
        Assert.Contains(buckets, line => line.Contains("le=\"3\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_refused_bid_is_told_apart_from_an_accepted_one_on_the_scrape()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 1m });

        var scrape = await ScrapeContainingAsync("outcome=\"BidTooLow\"");

        Assert.Contains("outcome=\"BidTooLow\"", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_runtime_and_request_instruments_come_along_for_free()
    {
        using var client = _fixture.CreateRawClient();
        await client.GetAsync(ApiRoutes.Auctions);

        var scrape = await ScrapeContainingAsync("http_server_request_duration");

        Assert.Contains("http_server_request_duration", scrape, StringComparison.Ordinal);
        Assert.Contains("dotnet_gc", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_outbox_backlog_is_on_the_scrape_for_the_dead_letter_alert()
    {
        var scrape = await ScrapeContainingAsync("takeauction_outbox_pending");

        Assert.Contains("takeauction_outbox_pending", scrape, StringComparison.Ordinal);
        Assert.Contains("takeauction_outbox_dead_letters", scrape, StringComparison.Ordinal);
        Assert.Contains("takeauction_outbox_oldest_pending_age_seconds", scrape, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_scrape_does_not_leak_a_bidder_s_sealed_ceiling()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 987654m })).EnsureSuccessStatusCode();

        var scrape = await ScrapeContainingAsync("outcome=\"accepted\"");

        Assert.DoesNotContain("987654", scrape, StringComparison.Ordinal);
    }

    // The exporter caches a scrape for a few hundred milliseconds, so the first read after an
    // action can still be the one taken before it.
    private async Task<string> ScrapeContainingAsync(string expected)
    {
        using var client = _fixture.CreateRawClient();

        var scrape = await client.GetStringAsync("/metrics");
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (!scrape.Contains(expected, StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
            scrape = await client.GetStringAsync("/metrics");
        }

        return scrape;
    }
}
