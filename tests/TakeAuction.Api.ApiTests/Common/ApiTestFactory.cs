using Microsoft.AspNetCore.Mvc.Testing;

namespace TakeAuction.Api.ApiTests.Common;

public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string NeverFiringCron = "0 0 1 1 *";

    public static readonly IReadOnlyDictionary<string, string> StaticSettings = new Dictionary<string, string>
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Production",
        ["Cache__InstanceName"] = "takeauction-api-tests:",
        ["Cache__AuctionListTtlSeconds"] = "60",
        ["Cache__AuctionDetailTtlSeconds"] = "60",
        ["Jwt__Issuer"] = "TakeAuction",
        ["Jwt__Audience"] = "TakeAuction.Client",
        ["Jwt__SigningKey"] = "api-test-signing-key-at-least-32-characters",
        ["Jwt__AccessTokenLifetimeMinutes"] = "15",
        ["AuthCookies__SecureAlways"] = "false",
        ["AuthCookies__SameSite"] = "Lax",
        ["RateLimiting__PermitLimit"] = "1000000",
        ["RateLimiting__AuthPermitLimit"] = "1000000",
        ["Seed__Enabled"] = "false",
        ["Jobs__ServerEnabled"] = "false",
        ["Jobs__DashboardEnabled"] = "true",
        ["Jobs__QueuePollIntervalSeconds"] = "1",
        ["Jobs__ExpireAuctionsCron"] = NeverFiringCron,
        ["Telemetry__PrometheusEndpointEnabled"] = "true"
    };
}
