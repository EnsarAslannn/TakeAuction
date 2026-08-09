using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.GetAuctions;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Auctions;

[Collection(IntegrationTestCollection.Name)]
public sealed class CreateAuctionTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/auctions";

    private readonly IntegrationTestFixture _fixture;

    public CreateAuctionTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_anonymous_callers()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_bidders()
    {
        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Creates_the_auction_for_a_seller()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);
        var request = ValidRequest();

        var response = await client.PostAsJsonAsync(Endpoint, request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateAuctionResponse>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(nameof(AuctionStatus.Scheduled), created.Status);
        Assert.Equal($"/api/v1/auctions/{created.Id}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Persists_the_auction_against_the_authenticated_seller()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);
        var request = ValidRequest();

        var response = await client.PostAsJsonAsync(Endpoint, request);
        response.EnsureSuccessStatusCode();

        var persisted = await _fixture.ExecuteDbContextAsync(db => db.Auctions.SingleAsync());

        Assert.Equal(seller.Id, persisted.SellerId);
        Assert.Equal(request.Title, persisted.Title);
        Assert.Equal(request.StartingPrice, persisted.StartingPrice);
        Assert.Equal(request.StartingPrice, persisted.CurrentPrice);
        Assert.Equal(request.MinimumBidIncrement, persisted.MinimumBidIncrement);
    }

    [Fact]
    public async Task Returns_a_validation_problem_for_an_invalid_payload()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest() with
        {
            Title = "no",
            StartingPrice = 0m,
            EndsAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateAuctionCommand.Title), problem.Errors.Keys);
        Assert.Contains(nameof(CreateAuctionCommand.StartingPrice), problem.Errors.Keys);
        Assert.Contains(nameof(CreateAuctionCommand.EndsAtUtc), problem.Errors.Keys);
        Assert.Empty(await _fixture.ExecuteDbContextAsync(db => db.Auctions.ToListAsync()));
    }

    [Fact]
    public async Task Invalidates_the_cached_auction_list()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var beforeCreate = await client.GetFromJsonAsync<PagedResult<AuctionListItem>>(
            Endpoint,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(beforeCreate);
        Assert.Equal(0, beforeCreate.TotalCount);

        var response = await client.PostAsJsonAsync(Endpoint, ValidRequest());
        response.EnsureSuccessStatusCode();

        var afterCreate = await client.GetFromJsonAsync<PagedResult<AuctionListItem>>(
            Endpoint,
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(afterCreate);
        Assert.Equal(1, afterCreate.TotalCount);
    }

    private static CreateAuctionRequest ValidRequest() => new(
        "Vintage mechanical watch",
        "A fully serviced 1968 mechanical watch with original box and papers.",
        1500.00m,
        25.00m,
        DateTimeOffset.UtcNow.AddHours(1),
        DateTimeOffset.UtcNow.AddDays(3));
}
