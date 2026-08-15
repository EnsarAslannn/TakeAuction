namespace TakeAuction.Api.Common.Observability;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Serves the collected metrics for scraping. Off by default: the endpoint is unauthenticated
    /// and belongs behind the gateway, not on the open internet.
    /// </summary>
    public bool PrometheusEndpointEnabled { get; set; }

    public string PrometheusPath { get; set; } = "/metrics";

    /// <summary>
    /// Where traces and metrics are shipped. With no endpoint configured nothing is exported —
    /// the instruments still run, so a scrape or a test can read them, but the process does not
    /// spend its time dialling a collector that is not there.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    public double TraceSampleRatio { get; set; } = 1.0;
}
