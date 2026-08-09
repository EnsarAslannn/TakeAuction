using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class GetAuctionByIdTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public GetAuctionByIdTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Returns_a_problem_document_for_an_unknown_auction()
    {
        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/auctions/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Returns_the_auction_with_the_seller_display_name()
    {
        var (auctionId, seller) = await SeedAsync();
        var client = _fixture.CreateClient();

        var auction = await client.GetFromJsonAsync<AuctionDetailResponse>(
            $"/api/v1/auctions/{auctionId}",
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(auction);
        Assert.Equal(auctionId, auction.Id);
        Assert.Equal("Rare stamp collection", auction.Title);
        Assert.Equal(seller.Id, auction.SellerId);
        Assert.Equal(seller.DisplayName, auction.SellerDisplayName);
        Assert.Equal(nameof(AuctionStatus.Active), auction.Status);
        Assert.Equal(100m, auction.StartingPrice);
        Assert.Equal(100m, auction.CurrentPrice);
    }

    [Fact]
    public async Task Serves_repeat_requests_from_redis()
    {
        var (auctionId, _) = await SeedAsync();
        var client = _fixture.CreateClient();

        await client.GetAsync($"/api/v1/auctions/{auctionId}");

        await _fixture.ExecuteDbContextAsync(async db =>
        {
            var auction = await db.Auctions.SingleAsync(entity => entity.Id == auctionId);
            db.Auctions.Remove(auction);
            return await db.SaveChangesAsync();
        });

        var response = await client.GetAsync($"/api/v1/auctions/{auctionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<(Guid AuctionId, User Seller)> SeedAsync()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller, "Auction House Ltd");

        var auctionId = await _fixture.ExecuteDbContextAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var auction = Auction.Create(
                seller.Id,
                "Rare stamp collection",
                "A detailed description of the lot on offer.",
                100m,
                5m,
                now,
                now.AddDays(2),
                now);

            db.Auctions.Add(auction);
            await db.SaveChangesAsync();

            return auction.Id;
        });

        return (auctionId, seller);
    }
}
