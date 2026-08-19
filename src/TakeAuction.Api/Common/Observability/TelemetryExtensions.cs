using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TakeAuction.Api.Common.Observability;

public static class TelemetryExtensions
{
    public static IServiceCollection AddTakeAuctionTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(TelemetryOptions.SectionName);

        services.AddOptions<TelemetryOptions>().Bind(section);

        var options = section.Get<TelemetryOptions>() ?? new TelemetryOptions();

        services.AddMetrics();
        services.AddSingleton<TakeAuctionTelemetry>();

        if (!options.Enabled)
        {
            return services;
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(TakeAuctionTelemetry.ServiceName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName)]))
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(TakeAuctionTelemetry.MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (options.PrometheusEndpointEnabled)
                {
                    metrics.AddPrometheusExporter();
                }

                AddOtlp(options, metrics);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new TraceIdRatioBasedSampler(options.TraceSampleRatio))
                    .AddSource(TakeAuctionTelemetry.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(instrumentation =>
                        instrumentation.Filter = context => !IsPlumbing(context.Request.Path, options))
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
                }
            });

        return services;
    }

    public static WebApplication MapTakeAuctionTelemetry(this WebApplication app)
    {
        var options = app.Configuration
            .GetSection(TelemetryOptions.SectionName)
            .Get<TelemetryOptions>() ?? new TelemetryOptions();

        if (options is { Enabled: true, PrometheusEndpointEnabled: true })
        {
            app.MapPrometheusScrapingEndpoint(options.PrometheusPath);
        }

        return app;
    }

    private static void AddOtlp(TelemetryOptions options, MeterProviderBuilder metrics)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            metrics.AddOtlpExporter(exporter => exporter.Endpoint = new Uri(options.OtlpEndpoint));
        }
    }

    private static bool IsPlumbing(PathString path, TelemetryOptions options) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments(options.PrometheusPath);
}
