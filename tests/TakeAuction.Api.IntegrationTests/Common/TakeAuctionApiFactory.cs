using Microsoft.AspNetCore.Mvc.Testing;

namespace TakeAuction.Api.IntegrationTests.Common;

public sealed class TakeAuctionApiFactory : WebApplicationFactory<Program>
{
    public const string NeverFiringCron = "0 0 1 1 *";

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
        ["RateLimiting__MediaUploadPermitLimit"] = "1000000",
        ["Seed__Enabled"] = "false",
        ["Jobs__ServerEnabled"] = "true",
        ["Jobs__WorkerCount"] = "2",
        ["Jobs__QueuePollIntervalSeconds"] = "1",
        ["Jobs__ExpireAuctionsCron"] = TakeAuctionApiFactory.NeverFiringCron,
        ["Jobs__PurgeRefreshTokensCron"] = TakeAuctionApiFactory.NeverFiringCron,
        ["Jobs__PurgeOutboxCron"] = TakeAuctionApiFactory.NeverFiringCron,

        ["Outbox__PollIntervalSeconds"] = "3600"
    };
}
