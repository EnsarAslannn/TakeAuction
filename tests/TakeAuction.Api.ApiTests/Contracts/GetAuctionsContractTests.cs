using System.Net;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctions;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class GetAuctionsContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    private ApiSession _seller = null!;

    public GetAuctionsContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _seller = await _fixture.CreateSellerAsync("Listing Seller");
    }

    public Task DisposeAsync()
    {
        _seller.Dispose();

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_listing_is_open_to_anonymous_callers()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync(ApiRoutes.Auctions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_empty_shelf_still_returns_a_well_formed_envelope()
    {
        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedAuctions>(await session.GetAsync(ApiRoutes.Auctions));

        Assert.Empty(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(0, page.TotalPages);
        Assert.False(page.HasNextPage);
    }

    [Fact]
    public async Task Every_list_item_carries_the_fields_the_gallery_renders()
    {
        await _fixture.CreateOpenAuctionAsync(_seller, startingPrice: 750m);

        using var session = _fixture.CreateSession();
        var page = await session.ReadAsync<PagedAuctions>(await session.GetAsync(ApiRoutes.Auctions));

        var item = Assert.Single(page.Items);

        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.False(string.IsNullOrWhiteSpace(item.Title));
        Assert.Null(item.ImageUrl);
        Assert.Equal(750m, item.StartingPrice);
        Assert.Equal(750m, item.CurrentPrice);
        Assert.Equal(nameof(AuctionStatus.Active), item.Status);
        Assert.True(item.EndsAtUtc > item.StartsAtUtc);
        Assert.Equal(_seller.UserId, item.SellerId);
    }

    [Fact]
    public async Task Paging_reports_the_page_the_caller_asked_for()
    {
        for (var index = 0; index < 5; index++)
        {
            await CreateAuctionTitledAsync($"Lot {index:00}");
        }

        using var session = _fixture.CreateSession();

        var first = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?page=1&pageSize=2"));
        var last = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?page=3&pageSize=2"));

        Assert.Equal(2, first.Items.Count);
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
        Assert.True(first.HasNextPage);

        Assert.Single(last.Items);
        Assert.False(last.HasNextPage);
    }

    [Fact]
    public async Task An_oversized_page_size_is_clamped_rather_than_refused()
    {
        await CreateAuctionTitledAsync("Only lot");

        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?page=0&pageSize=5000"));

        Assert.Equal(1, page.Page);
        Assert.Equal(GetAuctionsQuery.MaxPageSize, page.PageSize);
    }

    [Fact]
    public async Task The_status_filter_narrows_the_shelf()
    {
        await _fixture.CreateOpenAuctionAsync(_seller);
        (await _seller.PostAsync(ApiRoutes.Auctions, ApiTestFixture.ScheduledAuctionRequest()))
            .EnsureSuccessStatusCode();

        using var session = _fixture.CreateSession();

        var active = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?status={nameof(AuctionStatus.Active)}"));
        var scheduled = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?status={nameof(AuctionStatus.Scheduled)}"));

        Assert.Equal(nameof(AuctionStatus.Active), Assert.Single(active.Items).Status);
        Assert.Equal(nameof(AuctionStatus.Scheduled), Assert.Single(scheduled.Items).Status);
    }

    [Fact]
    public async Task The_search_filter_matches_on_the_title()
    {
        await CreateAuctionTitledAsync("Art deco cigarette case");
        await CreateAuctionTitledAsync("Victorian writing slope");

        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?search=deco"));

        Assert.Equal("Art deco cigarette case", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task The_seller_filter_isolates_one_consignor()
    {
        await CreateAuctionTitledAsync("Mine");

        using var other = await _fixture.CreateSellerAsync("Other Seller");
        await _fixture.CreateOpenAuctionAsync(other);

        using var session = _fixture.CreateSession();

        var page = await session.ReadAsync<PagedAuctions>(
            await session.GetAsync($"{ApiRoutes.Auctions}?sellerId={_seller.UserId}"));

        Assert.Equal("Mine", Assert.Single(page.Items).Title);
    }

    [Fact]
    public async Task A_search_that_matches_nothing_returns_an_empty_page_not_an_error()
    {
        await CreateAuctionTitledAsync("Art deco cigarette case");

        using var session = _fixture.CreateSession();
        var response = await session.GetAsync($"{ApiRoutes.Auctions}?search=zzzz-no-such-lot");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await session.ReadAsync<PagedAuctions>(response);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    private async Task CreateAuctionTitledAsync(string title)
    {
        var response = await _seller.PostAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest(title));
        response.EnsureSuccessStatusCode();
    }
}
