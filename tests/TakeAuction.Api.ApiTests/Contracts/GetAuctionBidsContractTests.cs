using System.Net;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Auctions.GetAuctionBids;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class GetAuctionBidsContractTests : IAsyncLifetime
{
    private const decimal StartingPrice = 100m;
    private const decimal Increment = 10m;

    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private Guid _auctionId;

    public GetAuctionBidsContractTests(ApiTestFixture fixture) => _fixture = fixture;

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
    public async Task The_history_is_open_to_anonymous_visitors()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync(ApiRoutes.Bids(_auctionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_lot_with_no_bids_answers_with_an_empty_page()
    {
        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedBids>(await session.GetAsync(ApiRoutes.Bids(_auctionId)));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public async Task An_unknown_auction_is_a_problem_document_not_an_empty_page()
    {
        using var session = _fixture.CreateSession();
        var missingId = Guid.CreateVersion7();

        var response = await session.GetAsync(ApiRoutes.Bids(missingId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await session.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);
    }

    [Fact]
    public async Task Every_entry_carries_what_the_feed_renders()
    {
        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        using var session = _fixture.CreateSession();
        var page = await session.ReadAsync<PagedBids>(await session.GetAsync(ApiRoutes.Bids(_auctionId)));

        var item = Assert.Single(page.Items);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(StartingPrice, item.Amount);
        Assert.False(item.IsAutomatic);
        Assert.Equal(bidder.UserId, item.BidderId);
        Assert.True(item.PlacedAtUtc <= DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task The_ladder_comes_back_highest_first()
    {
        // Each ceiling only pays one increment over the one before it, so the ladder the feed
        // shows is not the ladder of ceilings that produced it.
        await PlaceLadderAsync(150m, 200m, 250m);

        using var session = _fixture.CreateSession();
        var page = await session.ReadAsync<PagedBids>(await session.GetAsync(ApiRoutes.Bids(_auctionId)));

        Assert.Equal([210m, 160m, 100m], page.Items.Select(item => item.Amount));
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task The_history_can_be_paged()
    {
        await PlaceLadderAsync(150m, 200m, 250m, 300m, 350m);

        using var session = _fixture.CreateSession();

        var first = await session.ReadAsync<PagedBids>(
            await session.GetAsync($"{ApiRoutes.Bids(_auctionId)}?page=1&pageSize=2"));
        var last = await session.ReadAsync<PagedBids>(
            await session.GetAsync($"{ApiRoutes.Bids(_auctionId)}?page=3&pageSize=2"));

        Assert.Equal([310m, 260m], first.Items.Select(item => item.Amount));
        Assert.Equal(3, first.TotalPages);
        Assert.True(first.HasNextPage);

        Assert.Equal([100m], last.Items.Select(item => item.Amount));
        Assert.False(last.HasNextPage);
    }

    [Fact]
    public async Task An_oversized_page_size_is_clamped_rather_than_refused()
    {
        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedBids>(
            await session.GetAsync($"{ApiRoutes.Bids(_auctionId)}?page=0&pageSize=5000"));

        Assert.Equal(1, page.Page);
        Assert.Equal(GetAuctionBidsQuery.MaxPageSize, page.PageSize);
    }

    /// <summary>
    /// The history is a public record of the bidding, not of the bidders — the salon shows
    /// what was bid and when, and never puts a name to it.
    /// </summary>
    [Fact]
    public async Task The_history_names_nobody()
    {
        using var bidder = await _fixture.CreateBidderAsync("Very Identifiable Name");
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        using var session = _fixture.CreateSession();
        var response = await session.GetAsync(ApiRoutes.Bids(_auctionId));
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Very Identifiable Name", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("@takeauction.test", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bid_shows_up_in_the_history_the_moment_it_is_accepted()
    {
        using var session = _fixture.CreateSession();

        var before = await session.ReadAsync<PagedBids>(await session.GetAsync(ApiRoutes.Bids(_auctionId)));
        Assert.Equal(0, before.TotalCount);

        using var bidder = await _fixture.CreateBidderAsync();
        (await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount = 150m })).EnsureSuccessStatusCode();

        var after = await session.ReadAsync<PagedBids>(await session.GetAsync(ApiRoutes.Bids(_auctionId)));

        Assert.Equal(1, after.TotalCount);
    }

    private async Task PlaceLadderAsync(params decimal[] amounts)
    {
        foreach (var amount in amounts)
        {
            using var bidder = await _fixture.CreateBidderAsync();
            var response = await bidder.PostAsync(ApiRoutes.Bids(_auctionId), new { amount });

            response.EnsureSuccessStatusCode();
        }
    }
}
