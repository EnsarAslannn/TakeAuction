using TakeAuction.Api.ApiTests.Common;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class SecurityHeadersContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public SecurityHeadersContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_response_refuses_content_sniffing_and_framing()
    {
        using var client = _fixture.CreateRawClient();

        using var response = await client.GetAsync($"{ApiRoutes.Auctions}?pageSize=1");

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(response, "Referrer-Policy"));
        Assert.Equal("same-origin", Header(response, "Cross-Origin-Opener-Policy"));
        Assert.Contains("frame-ancestors 'none'", Header(response, "Content-Security-Policy"));
    }

    [Fact]
    public async Task A_rejected_request_is_covered_too()
    {
        using var client = _fixture.CreateRawClient();

        using var response = await client.GetAsync(ApiRoutes.DiagnosticsInfo);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(response, "X-Frame-Options"));
    }

    [Fact]
    public async Task An_uploaded_image_is_served_with_sniffing_switched_off()
    {
        using var client = _fixture.CreateRawClient();

        using var response = await client.GetAsync("/uploads/auctions/does-not-exist.png");

        Assert.Equal("nosniff", Header(response, "X-Content-Type-Options"));
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? string.Join(", ", values)
            : string.Empty;
}
