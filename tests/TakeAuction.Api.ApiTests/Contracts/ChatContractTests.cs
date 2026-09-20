using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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

    private static async Task<WebApplication> StartChatApiAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddChatFeature();

        var app = builder.Build();
        app.MapUnversionedChatEndpoint();
        new PostChatEndpoint().MapEndpoint(app.MapGroup("/api/v1"));
        await app.StartAsync();
        return app;
    }
}
