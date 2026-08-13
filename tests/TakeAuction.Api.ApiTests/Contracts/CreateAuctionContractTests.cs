using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.GetAuctionById;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class CreateAuctionContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public CreateAuctionContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Anonymous_callers_are_challenged()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.PostAsJsonAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bidders_are_forbidden_from_listing_a_lot()
    {
        using var bidder = await _fixture.CreateBidderAsync();

        var response = await bidder.PostAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_seller_creates_a_scheduled_auction_and_gets_its_location()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, ApiTestFixture.ScheduledAuctionRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await seller.ReadAsync<CreateAuctionResponse>(response);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(nameof(AuctionStatus.Scheduled), created.Status);
        Assert.Null(created.ImageUrl);
        LocationAssert.PointsAt(ApiRoutes.Auction(created.Id), response.Headers.Location);
    }

    [Fact]
    public async Task An_auction_whose_window_is_already_open_is_created_active()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, ApiTestFixture.OpenAuctionRequest());
        var created = await seller.ReadAsync<CreateAuctionResponse>(response);

        Assert.Equal(nameof(AuctionStatus.Active), created.Status);
    }

    [Fact]
    public async Task The_created_auction_is_readable_through_its_own_location()
    {
        using var seller = await _fixture.CreateSellerAsync("Vitrine Owner");

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Bauhaus desk lamp",
            description = "A restored 1930s desk lamp with its original enamel shade.",
            startingPrice = 2400.50m,
            minimumBidIncrement = 50m,
            startsAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(3)
        });

        var created = await seller.ReadAsync<CreateAuctionResponse>(response);

        var detailResponse = await seller.GetAsync(ApiRoutes.Auction(created.Id));
        var detail = await seller.ReadAsync<AuctionDetailResponse>(detailResponse);

        Assert.Equal(created.Id, detail.Id);
        Assert.Equal("Bauhaus desk lamp", detail.Title);
        Assert.Equal(2400.50m, detail.StartingPrice);
        Assert.Equal(2400.50m, detail.CurrentPrice);
        Assert.Equal(50m, detail.MinimumBidIncrement);
        Assert.Equal(2400.50m, detail.MinimumAcceptableBid);
        Assert.Equal(0, detail.BidCount);
        Assert.Equal(seller.UserId, detail.SellerId);
        Assert.Equal("Vitrine Owner", detail.SellerDisplayName);
    }

    [Theory]
    [InlineData("no", "too short to describe anything", nameof(CreateAuctionCommand.Title))]
    [InlineData("A perfectly fine title", "tiny", nameof(CreateAuctionCommand.Description))]
    public async Task Field_level_failures_come_back_as_a_validation_problem(
        string title,
        string description,
        string expectedKey)
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title,
            description,
            startingPrice = 100m,
            minimumBidIncrement = 5m,
            startsAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(expectedKey, problem.Errors.Keys);
    }

    [Fact]
    public async Task A_window_that_opened_in_the_past_is_rejected()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Backdated lot",
            description = "An auction whose window opened long before it was ever posted.",
            startingPrice = 100m,
            minimumBidIncrement = 5m,
            startsAtUtc = DateTimeOffset.UtcNow.AddHours(-4),
            endsAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(CreateAuctionCommand.StartsAtUtc), problem.Errors.Keys);
    }

    [Fact]
    public async Task A_window_shorter_than_the_minimum_run_is_rejected()
    {
        using var seller = await _fixture.CreateSellerAsync();
        var startsAt = DateTimeOffset.UtcNow.AddMinutes(10);

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Blink and miss it",
            description = "An auction that would open and close inside a single minute.",
            startingPrice = 100m,
            minimumBidIncrement = 5m,
            startsAtUtc = startsAt,
            endsAtUtc = startsAt.AddMinutes(1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(CreateAuctionCommand.EndsAtUtc), problem.Errors.Keys);
    }

    [Fact]
    public async Task A_window_that_closes_before_it_opens_is_rejected()
    {
        using var seller = await _fixture.CreateSellerAsync();
        var startsAt = DateTimeOffset.UtcNow.AddHours(2);

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Inside out window",
            description = "An auction whose closing time comes before its opening time.",
            startingPrice = 100m,
            minimumBidIncrement = 5m,
            startsAtUtc = startsAt,
            endsAtUtc = startsAt.AddHours(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(CreateAuctionCommand.EndsAtUtc), problem.Errors.Keys);
    }

    [Fact]
    public async Task Money_fields_are_held_to_two_decimal_places()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Over precise lot",
            description = "An auction priced to more decimal places than money has.",
            startingPrice = 100.12345m,
            minimumBidIncrement = 5.6789m,
            startsAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(CreateAuctionCommand.StartingPrice), problem.Errors.Keys);
        Assert.Contains(nameof(CreateAuctionCommand.MinimumBidIncrement), problem.Errors.Keys);
    }

    [Fact]
    public async Task An_image_url_that_was_not_uploaded_here_is_refused()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var response = await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "Hotlinked lot",
            description = "An auction pointing its image at somebody else's server.",
            startingPrice = 100m,
            minimumBidIncrement = 5m,
            startsAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            imageUrl = "https://example.com/not-ours.jpg"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await seller.ReadAsync<ValidationProblemDetails>(response);

        Assert.Contains(nameof(CreateAuctionCommand.ImageUrl), problem.Errors.Keys);
    }

    [Fact]
    public async Task A_rejected_payload_leaves_nothing_behind_in_the_listing()
    {
        using var seller = await _fixture.CreateSellerAsync();

        await seller.PostAsync(ApiRoutes.Auctions, new
        {
            title = "no",
            description = "tiny",
            startingPrice = 0m,
            minimumBidIncrement = 0m,
            startsAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
            endsAtUtc = DateTimeOffset.UtcNow.AddDays(1)
        });

        var listing = await seller.GetAsync(ApiRoutes.Auctions);
        var page = await seller.ReadAsync<PagedAuctions>(listing);

        Assert.Equal(0, page.TotalCount);
    }
}
