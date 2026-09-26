using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Features.Chat;

namespace TakeAuction.Api.ApiTests.Contracts;

public sealed class ChatContractTests
{
    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/v1/chat")]
    public async Task A_guest_can_ask_a_site_question_without_an_ai_service(string path)
    {
        await using var app = await StartChatApiAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(path, new
        {
            message = "Teklif nasıl verilir?",
            language = "tr",
            history = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        JsonAssert.HasProperties(body, "answer", "sources", "suggestions", "usedAi");
        Assert.False(body.GetProperty("usedAi").GetBoolean());
        Assert.InRange(body.GetProperty("suggestions").GetArrayLength(), 1, 3);
        Assert.Equal("/#how-it-works", body.GetProperty("sources")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task A_blank_chat_message_is_a_bad_request()
    {
        await using var app = await StartChatApiAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            message = " ",
            language = "tr",
            history = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_contextual_question_returns_the_server_side_auction_snapshot()
    {
        await using var app = await StartChatApiAsync();
        using var client = app.GetTestClient();
        var auctionId = StubAuctionContextReader.Auction.Id;

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            message = "Bu lotun durumu nedir?",
            language = "tr",
            history = Array.Empty<object>(),
            context = new { path = $"/auctions/{auctionId}", auctionId }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains("Sunucudaki Lot", body.GetProperty("answer").GetString());
        Assert.Equal($"/auctions/{auctionId}", body.GetProperty("sources")[0].GetProperty("url").GetString());
    }

    private static async Task<WebApplication> StartChatApiAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IChatAuctionContextReader, StubAuctionContextReader>();
        builder.Services.AddChatFeature();

        var app = builder.Build();
        app.MapUnversionedChatEndpoint();
        new PostChatEndpoint().MapEndpoint(app.MapGroup("/api/v1"));
        await app.StartAsync();
        return app;
    }

    private sealed class StubAuctionContextReader : IChatAuctionContextReader
    {
        public static readonly ChatAuctionContext Auction = new(
            Guid.Parse("018f6f47-4dd2-7c97-8f58-1f70edc78a21"),
            "Sunucudaki Lot",
            "Active",
            1_000m,
            1_100m,
            new DateTimeOffset(2026, 9, 26, 18, 30, 0, TimeSpan.Zero));

        public Task<ChatAuctionContext?> FindAsync(Guid auctionId, CancellationToken cancellationToken) =>
            Task.FromResult<ChatAuctionContext?>(auctionId == Auction.Id ? Auction : null);
    }
}
