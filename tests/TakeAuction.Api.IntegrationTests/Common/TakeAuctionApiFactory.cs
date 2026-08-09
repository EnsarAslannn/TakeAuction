using Microsoft.AspNetCore.Mvc.Testing;

namespace TakeAuction.Api.IntegrationTests.Common;

public sealed class TakeAuctionApiFactory : WebApplicationFactory<Program>
{
    public static readonly IReadOnlyDictionary<string, string> StaticSettings = new Dictionary<string, string>
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Production",
        ["Cache__InstanceName"] = "takeauction-tests:",
        ["Cache__AuctionListTtlSeconds"] = "60",
        ["Cache__AuctionDetailTtlSeconds"] = "60",
        ["Jwt__Issuer"] = "TakeAuction",
        ["Jwt__Audience"] = "TakeAuction.Client",
        ["Jwt__SigningKey"] = "integration-test-signing-key-at-least-32-chars",
        ["Jwt__AccessTokenLifetimeMinutes"] = "15",
        ["RateLimiting__PermitLimit"] = "1000000",
        ["RateLimiting__AuthPermitLimit"] = "1000000",
        ["Seed__Enabled"] = "false"
    };
}
