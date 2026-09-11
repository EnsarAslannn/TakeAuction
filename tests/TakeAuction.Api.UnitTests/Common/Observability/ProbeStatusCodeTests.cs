using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TakeAuction.Api.Common.Observability;

namespace TakeAuction.Api.UnitTests.Common.Observability;

public sealed class ProbeStatusCodeTests
{
    [Fact]
    public void A_lost_broker_leaves_the_instance_in_rotation()
    {
        Assert.Equal(
            StatusCodes.Status200OK,
            HealthCheckExtensions.ProbeStatusCodes[HealthStatus.Degraded]);
    }

    [Fact]
    public void A_store_the_api_cannot_serve_without_takes_the_instance_out()
    {
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            HealthCheckExtensions.ProbeStatusCodes[HealthStatus.Unhealthy]);
    }

    [Fact]
    public void A_healthy_report_answers_plainly()
    {
        Assert.Equal(
            StatusCodes.Status200OK,
            HealthCheckExtensions.ProbeStatusCodes[HealthStatus.Healthy]);
    }

    [Fact]
    public void Every_status_the_framework_can_report_is_mapped()
    {
        Assert.All(
            Enum.GetValues<HealthStatus>(),
            status => Assert.True(
                HealthCheckExtensions.ProbeStatusCodes.ContainsKey(status),
                $"Health status {status} has no status code, so the probe would fall back to a default."));
    }
}
