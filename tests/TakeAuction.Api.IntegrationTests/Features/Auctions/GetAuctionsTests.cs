using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.GetAuctions;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetAuctionsTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auctions";

    private readonly IntegrationTestFixture _fixture;

    public GetAuctionsTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Is_reachable_anonymously_and_returns_an_empty_page()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResult<AuctionListItem>>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task Returns_the_seeded_auctions_newest_first()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var page = await GetPageAsync(Endpoint);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Newest lot", page.Items[0].Title);
    }

    [Fact]
    public async Task Pages_the_result_set()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var firstPage = await GetPageAsync($"{Endpoint}?page=1&pageSize=2");
        var secondPage = await GetPageAsync($"{Endpoint}?page=2&pageSize=2");

        Assert.Equal(2, firstPage.Items.Count);
        Assert.Single(secondPage.Items);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.True(firstPage.HasNextPage);
        Assert.False(secondPage.HasNextPage);
    }

    [Fact]
    public async Task Filters_by_status()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var page = await GetPageAsync($"{Endpoint}?status={nameof(AuctionStatus.Scheduled)}");

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, item => Assert.Equal(nameof(AuctionStatus.Scheduled), item.Status));
    }

    [Fact]
    public async Task Filters_by_seller()
    {
        var sellerA = await _fixture.CreateUserAsync(UserRole.Seller);
        var sellerB = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(sellerA.Id);
        await SeedAuctionsAsync(sellerB.Id, titlePrefix: "B ");

        var page = await GetPageAsync($"{Endpoint}?sellerId={sellerB.Id}");

        Assert.Equal(3, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(sellerB.Id, item.SellerId));
    }

    [Fact]
    public async Task Search_matches_titles_case_insensitively()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var page = await GetPageAsync($"{Endpoint}?search=RARE");

        var item = Assert.Single(page.Items);
        Assert.Equal("Rare stamp collection", item.Title);
    }

    [Fact]
    public async Task Serves_repeat_requests_from_redis()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var firstPage = await GetPageAsync(Endpoint);

        await _fixture.ExecuteDbContextAsync(async db =>
        {
            db.Auctions.Add(NewAuction(seller.Id, "Written straight to the database"));
            return await db.SaveChangesAsync();
        });

        var secondPage = await GetPageAsync(Endpoint);

        Assert.Equal(firstPage.TotalCount, secondPage.TotalCount);
        Assert.DoesNotContain(secondPage.Items, item => item.Title == "Written straight to the database");
    }

    [Fact]
    public async Task Caches_each_filter_combination_separately()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        await SeedAuctionsAsync(seller.Id);

        var allAuctions = await GetPageAsync(Endpoint);
        var searchResults = await GetPageAsync($"{Endpoint}?search=RARE");

        Assert.Equal(3, allAuctions.TotalCount);
        Assert.Equal(1, searchResults.TotalCount);
    }

    private async Task<PagedResult<AuctionListItem>> GetPageAsync(string url)
    {
        var client = _fixture.CreateClient();

        var page = await client.GetFromJsonAsync<PagedResult<AuctionListItem>>(
            url,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(page);

        return page;
    }

    private Task SeedAuctionsAsync(Guid sellerId, string titlePrefix = "") =>
        _fixture.ExecuteDbContextAsync(async db =>
        {
            db.Auctions.AddRange(
                NewAuction(sellerId, $"{titlePrefix}Rare stamp collection", TimeSpan.FromMinutes(-30)),
                NewAuction(sellerId, $"{titlePrefix}Antique oak desk", TimeSpan.FromMinutes(-20), scheduled: true),
                NewAuction(sellerId, $"{titlePrefix}Newest lot", TimeSpan.FromMinutes(-10)));

            return await db.SaveChangesAsync();
        });

    private static Auction NewAuction(
        Guid sellerId,
        string title,
        TimeSpan? createdOffset = null,
        bool scheduled = false)
    {
        var createdAtUtc = DateTimeOffset.UtcNow.Add(createdOffset ?? TimeSpan.Zero);

        return Auction.Create(
            sellerId,
            title,
            "A detailed description of the lot on offer.",
            100m,
            5m,
            scheduled ? createdAtUtc.AddHours(6) : createdAtUtc,
            createdAtUtc.AddDays(2),
            createdAtUtc);
    }
}
