using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Watchlist.GetWatchlist;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class WatchlistContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;
    private ApiSession _watcher = null!;
    private Guid _auctionId;

    public WatchlistContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Watched Seller");
        _watcher = await _fixture.CreateBidderAsync("Watching Bidder");
        _auctionId = await _fixture.CreateOpenAuctionAsync(_seller);
    }

    public Task DisposeAsync()
    {
        _seller.Dispose();
        _watcher.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Anonymous_callers_are_challenged_on_every_watchlist_endpoint()
    {
        using var client = _fixture.CreateRawClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsync(ApiRoutes.Watch(_auctionId), null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(ApiRoutes.Watch(_auctionId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(ApiRoutes.Watchlist)).StatusCode);
    }

    [Fact]
    public async Task A_new_session_starts_with_an_empty_watchlist()
    {
        Assert.Empty(await WatchlistAsync(_watcher));
    }

    [Fact]
    public async Task Watching_a_lot_puts_it_on_the_watchlist()
    {
        var response = await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var item = Assert.Single(await WatchlistAsync(_watcher));
        Assert.Equal(_auctionId, item.Id);
        Assert.Equal("Active", item.Status);
        Assert.Equal("Live lot under test", item.Title);
    }

    [Fact]
    public async Task Watching_the_same_lot_twice_is_harmless()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await _watcher.PutAsync(ApiRoutes.Watch(_auctionId))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _watcher.PutAsync(ApiRoutes.Watch(_auctionId))).StatusCode);

        Assert.Single(await WatchlistAsync(_watcher));
    }

    [Fact]
    public async Task Unwatching_takes_the_lot_off_and_unwatching_again_is_harmless()
    {
        await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));

        Assert.Equal(HttpStatusCode.NoContent, (await _watcher.DeleteAsync(ApiRoutes.Watch(_auctionId))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _watcher.DeleteAsync(ApiRoutes.Watch(_auctionId))).StatusCode);

        Assert.Empty(await WatchlistAsync(_watcher));
    }

    [Fact]
    public async Task One_callers_watchlist_is_invisible_to_another()
    {
        await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));

        using var stranger = await _fixture.CreateBidderAsync("Stranger");

        Assert.Empty(await WatchlistAsync(stranger));
    }

    [Fact]
    public async Task Watching_a_lot_that_does_not_exist_is_a_404()
    {
        var response = await _watcher.PutAsync(ApiRoutes.Watch(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Watching_a_withdrawn_lot_is_a_409()
    {
        (await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { })).EnsureSuccessStatusCode();

        var response = await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ApiTestFixture.JsonOptions);
        Assert.Equal("The lot has already closed", problem!.Title);
    }

    [Fact]
    public async Task A_lot_withdrawn_after_it_was_watched_stays_on_the_list_as_cancelled()
    {
        await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));
        (await _seller.PostAsync(ApiRoutes.Cancel(_auctionId), new { })).EnsureSuccessStatusCode();

        var item = Assert.Single(await WatchlistAsync(_watcher));
        Assert.Equal("Cancelled", item.Status);
    }

    [Fact]
    public async Task Watching_without_the_csrf_token_is_refused()
    {
        var response = await _watcher.PutAsync(ApiRoutes.Watch(_auctionId), withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await WatchlistAsync(_watcher));
    }

    [Fact]
    public async Task Unwatching_without_the_csrf_token_is_refused()
    {
        await _watcher.PutAsync(ApiRoutes.Watch(_auctionId));

        var response = await _watcher.DeleteAsync(ApiRoutes.Watch(_auctionId), withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Single(await WatchlistAsync(_watcher));
    }

    private static async Task<List<WatchlistItem>> WatchlistAsync(ApiSession session)
    {
        var response = await session.GetAsync(ApiRoutes.Watchlist);
        response.EnsureSuccessStatusCode();

        return await session.ReadAsync<List<WatchlistItem>>(response);
    }
}
