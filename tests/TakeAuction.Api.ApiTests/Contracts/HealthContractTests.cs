using System.Net;
using System.Text.Json;
using TakeAuction.Api.ApiTests.Common;
using TakeAuction.Api.Common.Observability;

namespace TakeAuction.Api.ApiTests.Contracts;

[Collection(ApiTestCollection.Name)]
public sealed class HealthContractTests
{
    private readonly ApiTestFixture _fixture;

    public HealthContractTests(ApiTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(HealthCheckExtensions.LivePath)]
    [InlineData(HealthCheckExtensions.ReadyPath)]
    [InlineData(HealthCheckExtensions.LegacyPath)]
    public async Task Every_probe_answers_without_authentication(string path)
    {
        using var client = _fixture.CreateRawClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await ReadAsync(client, path);

        Assert.Equal("healthy", body.GetProperty("status").GetString());
        Assert.Equal(HealthCheckExtensions.ServiceName, body.GetProperty("service").GetString());
        JsonAssert.HasProperties(body, "environment", "totalDurationMs", "timestamp", "checks");
    }

    [Fact]
    public async Task Liveness_consults_no_dependency()
    {
        using var client = _fixture.CreateRawClient();

        var body = await ReadAsync(client, HealthCheckExtensions.LivePath);

        Assert.Empty(body.GetProperty("checks").EnumerateArray());
    }

    [Fact]
    public async Task Readiness_probes_the_stores_the_api_cannot_serve_without()
    {
        using var client = _fixture.CreateRawClient();

        var body = await ReadAsync(client, HealthCheckExtensions.ReadyPath);

        var names = ProbeNames(body);

        Assert.Contains("postgres", names);
        Assert.Contains("redis", names);
        Assert.Contains("masstransit-bus", names);
    }

    [Fact]
    public async Task Every_probe_reports_its_own_status_and_duration()
    {
        using var client = _fixture.CreateRawClient();

        var body = await ReadAsync(client, HealthCheckExtensions.ReadyPath);

        foreach (var check in body.GetProperty("checks").EnumerateArray())
        {
            JsonAssert.HasProperties(check, "name", "status", "durationMs");
            Assert.Equal("healthy", check.GetProperty("status").GetString());
            Assert.True(check.GetProperty("durationMs").GetDouble() >= 0);
        }
    }

    [Fact]
    public async Task A_probe_never_leaks_its_failure_detail_outside_development()
    {
        using var client = _fixture.CreateRawClient();

        var body = await ReadAsync(client, HealthCheckExtensions.ReadyPath);

        Assert.Equal("Production", body.GetProperty("environment").GetString());
        Assert.All(
            body.GetProperty("checks").EnumerateArray(),
            check => Assert.Equal(JsonValueKind.Null, check.GetProperty("error").ValueKind));
    }

    [Fact]
    public async Task The_probes_are_exempt_from_rate_limiting()
    {
        using var client = _fixture.CreateRawClient();

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var response = await client.GetAsync(HealthCheckExtensions.LivePath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<JsonElement> ReadAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return JsonAssert.Root(await response.Content.ReadAsStringAsync());
    }

    private static List<string> ProbeNames(JsonElement body) =>
        body.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString() ?? string.Empty)
            .ToList();
}
