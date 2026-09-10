using System.Text.Json;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class SealedCeilingContractTests : IAsyncLifetime
{
    private const string CeilingProperty = "maxAmount";
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 10m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public SealedCeilingContractTests(ApiTestFixture fixture) => _fixture = fixture;

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
    public async Task The_bid_history_carries_settled_prices_but_never_a_ceiling()
    {
        await RunADuelAsync();

        using var session = _fixture.CreateSession();
        var body = await ReadBodyAsync(session, ApiRoutes.Bids(_auctionId));

        Assert.DoesNotContain(CeilingProperty, body, StringComparison.OrdinalIgnoreCase);

        foreach (var entry in JsonDocument.Parse(body).RootElement.GetProperty("items").EnumerateArray())
        {
            Assert.False(
                entry.TryGetProperty(CeilingProperty, out _),
                "A bid history entry must never carry the bidder's ceiling.");
        }
    }

    [Fact]
    public async Task The_detail_endpoint_never_carries_a_ceiling()
    {
        await RunADuelAsync();

        using var session = _fixture.CreateSession();
        var body = await ReadBodyAsync(session, ApiRoutes.Auction(_auctionId));

        Assert.DoesNotContain(CeilingProperty, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_bidder_reads_back_only_their_own_ceiling()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 250m });
        response.EnsureSuccessStatusCode();

        var receipt = await bidder.ReadAsync<PlaceBidResponse>(response);

        Assert.Equal(250m, receipt.MaxAmount);
        Assert.Equal(StartingPrice, receipt.Amount);
    }

    [Fact]
    public async Task The_settled_price_may_sit_on_a_beaten_ceiling_which_is_how_a_proxy_duel_ends()
    {
        var (_, challengerCeiling) = await RunADuelAsync();

        using var session = _fixture.CreateSession();
        var body = await ReadBodyAsync(session, ApiRoutes.Bids(_auctionId));

        var amounts = JsonDocument.Parse(body).RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("amount").GetDecimal())
            .ToList();

        Assert.Contains(challengerCeiling, amounts);
        Assert.Contains(challengerCeiling + Increment, amounts);
    }

    private async Task<(decimal LeaderCeiling, decimal ChallengerCeiling)> RunADuelAsync()
    {
        const decimal leaderCeiling = 400m;
        const decimal challengerCeiling = 200m;

        using var leader = await _fixture.CreateBidderAsync();
        using var challenger = await _fixture.CreateBidderAsync();

        (await leader.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = leaderCeiling }))
            .EnsureSuccessStatusCode();

        (await challenger.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = challengerCeiling }))
            .EnsureSuccessStatusCode();

        return (leaderCeiling, challengerCeiling);
    }

    private static async Task<string> ReadBodyAsync(ApiSession session, string url)
    {
        var response = await session.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
