using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TakeAuction.Api.Domain.Users;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.Features.Auctions.GetAuctionById;
using TakeAuction.Api.Features.Media.UploadImage;
using TakeAuction.Api.IntegrationTests.Common;

namespace TakeAuction.Api.IntegrationTests.Features.Media;

[Collection(IntegrationTestCollection.Name)]
public sealed class UploadImageTests : IAsyncLifetime
{
    private const string Endpoint = "/api/v1/media/images";
    private const string AuctionsEndpoint = "/api/v1/auctions";

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private readonly IntegrationTestFixture _fixture;

    public UploadImageTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Rejects_anonymous_callers()
    {
        var client = _fixture.CreateClient();

        var response = await client.PostAsync(Endpoint, ImageContent(PngBytes));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_bidders()
    {
        var bidder = await _fixture.CreateUserAsync(UserRole.Bidder);
        var client = await _fixture.CreateClientAsAsync(bidder);

        var response = await client.PostAsync(Endpoint, ImageContent(PngBytes));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Stores_the_image_and_returns_a_servable_url()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var response = await client.PostAsync(Endpoint, ImageContent(PngBytes));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var uploaded = await response.Content.ReadFromJsonAsync<UploadImageResponse>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(uploaded);
        Assert.StartsWith("/uploads/auctions/", uploaded.Url, StringComparison.Ordinal);

        var fetched = await client.GetAsync(uploaded.Url);

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(PngBytes, await fetched.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Rejects_content_that_is_not_an_image()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var portableExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        var response = await client.PostAsync(Endpoint, ImageContent(portableExecutable));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_a_disallowed_content_type()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var response = await client.PostAsync(Endpoint, ImageContent(PngBytes, contentType: "image/svg+xml"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Uploaded_image_can_be_attached_to_a_new_auction()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var upload = await client.PostAsync(Endpoint, ImageContent(PngBytes));
        upload.EnsureSuccessStatusCode();

        var uploaded = await upload.Content.ReadFromJsonAsync<UploadImageResponse>(
            IntegrationTestFixture.JsonOptions);
        Assert.NotNull(uploaded);

        var created = await client.PostAsJsonAsync(AuctionsEndpoint, ValidRequest(uploaded.Url));
        created.EnsureSuccessStatusCode();

        var auction = await created.Content.ReadFromJsonAsync<CreateAuctionResponse>(
            IntegrationTestFixture.JsonOptions);
        Assert.NotNull(auction);
        Assert.Equal(uploaded.Url, auction.ImageUrl);

        var detail = await client.GetFromJsonAsync<AuctionDetailResponse>(
            $"{AuctionsEndpoint}/{auction.Id}",
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(detail);
        Assert.Equal(uploaded.Url, detail.ImageUrl);

        var stored = await _fixture.ExecuteDbContextAsync(db =>
            db.Auctions.SingleAsync(entity => entity.Id == auction.Id));

        Assert.Equal(uploaded.Url, stored.ImageUrl);
    }

    [Fact]
    public async Task Auction_without_an_image_is_still_accepted()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var created = await client.PostAsJsonAsync(AuctionsEndpoint, ValidRequest(imageUrl: null));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var auction = await created.Content.ReadFromJsonAsync<CreateAuctionResponse>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(auction);
        Assert.Null(auction.ImageUrl);
    }

    [Fact]
    public async Task Auction_referencing_an_external_image_is_rejected()
    {
        var seller = await _fixture.CreateUserAsync(UserRole.Seller);
        var client = await _fixture.CreateClientAsAsync(seller);

        var created = await client.PostAsJsonAsync(
            AuctionsEndpoint,
            ValidRequest("https://evil.example/tracker.png"));

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);

        var problem = await created.Content.ReadFromJsonAsync<ValidationProblemDetails>(
            IntegrationTestFixture.JsonOptions);

        Assert.NotNull(problem);
        Assert.Contains(nameof(CreateAuctionCommand.ImageUrl), problem.Errors.Keys);
    }

    private static MultipartFormDataContent ImageContent(byte[] bytes, string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return new MultipartFormDataContent { { file, "file", "photo.png" } };
    }

    private static CreateAuctionRequest ValidRequest(string? imageUrl) => new(
        "Vintage mechanical watch",
        "A fully serviced 1968 mechanical watch with original box and papers.",
        1500.00m,
        25.00m,
        DateTimeOffset.UtcNow.AddHours(1),
        DateTimeOffset.UtcNow.AddDays(3),
        imageUrl);
}
