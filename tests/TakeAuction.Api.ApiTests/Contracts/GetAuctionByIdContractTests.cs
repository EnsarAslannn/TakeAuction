using System.Net;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.GetAuctionById;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class GetAuctionByIdContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public GetAuctionByIdContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_detail_is_open_to_anonymous_callers_and_exposes_the_full_shape()
    {
        using var seller = await _fixture.CreateSellerAsync("Detail Seller");
        var auctionId = await _fixture.CreateOpenAuctionAsync(seller, startingPrice: 900m, minimumBidIncrement: 25m);

        using var session = _fixture.CreateSession();
        var response = await session.GetAsync(ApiRoutes.Auction(auctionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await session.ReadAsync<AuctionDetailResponse>(response);

        Assert.Equal(auctionId, detail.Id);
        Assert.False(string.IsNullOrWhiteSpace(detail.Description));
        Assert.Null(detail.ImageUrl);
        Assert.Equal(900m, detail.StartingPrice);
        Assert.Equal(900m, detail.CurrentPrice);
        Assert.Equal(25m, detail.MinimumBidIncrement);
        Assert.Equal(900m, detail.MinimumAcceptableBid);
        Assert.Equal(0, detail.BidCount);
        Assert.Equal(nameof(AuctionStatus.Active), detail.Status);
        Assert.Equal(seller.UserId, detail.SellerId);
        Assert.Equal("Detail Seller", detail.SellerDisplayName);
    }

    [Fact]
    public async Task An_unknown_id_comes_back_as_a_problem_document()
    {
        using var session = _fixture.CreateSession();
        var missingId = Guid.CreateVersion7();

        var response = await session.GetAsync(ApiRoutes.Auction(missingId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await session.ReadAsync<ProblemDetails>(response);

        Assert.Equal("Auction not found", problem.Title);
        Assert.Contains(missingId.ToString(), problem.Detail);
    }

    [Fact]
    public async Task An_id_that_is_not_a_guid_never_reaches_the_slice()
    {
        using var session = _fixture.CreateSession();

        var response = await session.GetAsync($"{ApiRoutes.Auctions}/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_second_read_is_served_from_cache_without_drifting_from_the_first()
    {
        using var seller = await _fixture.CreateSellerAsync();
        var auctionId = await _fixture.CreateOpenAuctionAsync(seller);

        using var session = _fixture.CreateSession();

        var first = await session.ReadAsync<AuctionDetailResponse>(
            await session.GetAsync(ApiRoutes.Auction(auctionId)));
        var second = await session.ReadAsync<AuctionDetailResponse>(
            await session.GetAsync(ApiRoutes.Auction(auctionId)));

        Assert.Equal(first, second);
    }
}
