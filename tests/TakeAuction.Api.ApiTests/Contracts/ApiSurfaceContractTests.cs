using System.Net;
using TakeAuction.Api.ApiTests.Common;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class ApiSurfaceContractTests : IAsyncLifetime
{
    private readonly ApiTestFixture _fixture;

    public ApiSurfaceContractTests(ApiTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_health_probe_answers_without_authentication()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonAssert.Root(await response.Content.ReadAsStringAsync());

        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal("TakeAuction.Api", body.GetProperty("service").GetString());
    }

    [Fact]
    public async Task The_diagnostics_endpoint_reports_the_resolved_version()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync("/api/v1/diagnostics/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonAssert.Root(await response.Content.ReadAsStringAsync());

        JsonAssert.HasProperties(body, "service", "apiVersion", "environment", "scheme", "timestamp");
        Assert.Equal("1", body.GetProperty("apiVersion").GetString());
    }

    [Fact]
    public async Task Versioned_endpoints_advertise_the_versions_they_support()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync(ApiRoutes.Auctions);

        Assert.True(
            response.Headers.TryGetValues("api-supported-versions", out var supported),
            "expected the API to report its supported versions");
        Assert.Contains("1.0", supported);
    }

    // The version lives in the route template, so an unserved version never resolves an
    // endpoint at all — routing turns it away before the versioning policy is consulted.
    [Fact]
    public async Task A_version_the_api_does_not_serve_resolves_no_endpoint()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync("/api/v9/auctions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_route_is_a_plain_not_found()
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync("/api/v1/there-is-no-such-slice");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_json_body_is_rejected_before_the_handler()
    {
        using var seller = await _fixture.CreateSellerAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Auctions)
        {
            Content = new StringContent("{ not json at all", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await seller.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
