namespace TakeAuction.Api.Common.Observability;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; } = true;

    public bool PrometheusEndpointEnabled { get; set; }

    public string PrometheusPath { get; set; } = "/metrics";

    public string? OtlpEndpoint { get; set; }

    public double TraceSampleRatio { get; set; } = 1.0;
}
