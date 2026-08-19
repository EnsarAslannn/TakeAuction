using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Common.Observability;

public static class HealthCheckExtensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string LegacyPath = "/health";

    public const string ServiceName = "TakeAuction.Api";

    public static IServiceCollection AddTakeAuctionHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var builder = services.AddHealthChecks();

        builder.AddDbContextCheck<AppDbContext>("postgres");

        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            builder.AddRedis(redisConnectionString, name: "redis");
        }

        return services;
    }

    public static IEndpointRouteBuilder MapTakeAuctionHealthChecks(this IEndpointRouteBuilder builder)
    {
        Map(builder, LivePath, "HealthLive", _ => false);

        Map(builder, ReadyPath, "HealthReady", _ => true);

        Map(builder, LegacyPath, "HealthCheck", _ => true);

        return builder;
    }

    private static void Map(
        IEndpointRouteBuilder builder,
        string path,
        string name,
        Func<HealthCheckRegistration, bool> predicate) =>
        builder.MapHealthChecks(path, new HealthCheckOptions
        {
            Predicate = predicate,
            ResponseWriter = WriteReportAsync
        })
        .WithName(name)
        .WithTags("Diagnostics")
        .AllowAnonymous()
        .DisableRateLimiting();

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        var environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

        var payload = new
        {
            status = Describe(report.Status),
            service = ServiceName,
            environment = environment.EnvironmentName,
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            timestamp = DateTimeOffset.UtcNow,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = Describe(entry.Value.Status),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                error = environment.IsDevelopment()
                    ? entry.Value.Exception?.Message ?? entry.Value.Description
                    : null
            })
        };

        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsJsonAsync(payload);
    }

    private static string Describe(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "unhealthy"
    };
}
