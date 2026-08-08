using Serilog;
using Serilog.Events;

namespace TakeAuction.Api.Common.Observability;

public static class SerilogExtensions
{
    public static IHostBuilder UseTakeAuctionSerilog(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "TakeAuction.Api")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));
    }

    public static IApplicationBuilder UseTakeAuctionRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

            options.GetLevel = (httpContext, elapsed, exception) => exception is not null
                ? LogEventLevel.Error
                : httpContext.Response.StatusCode > 499
                    ? LogEventLevel.Error
                    : httpContext.Response.StatusCode == StatusCodes.Status429TooManyRequests
                        ? LogEventLevel.Warning
                        : LogEventLevel.Information;

            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                diagnosticContext.Set("Scheme", httpContext.Request.Scheme);
                diagnosticContext.Set("Host", httpContext.Request.Host.Value ?? string.Empty);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());

                if (httpContext.User.Identity?.IsAuthenticated == true)
                {
                    diagnosticContext.Set("UserId", httpContext.User.Identity.Name ?? string.Empty);
                }
            };
        });
    }
}
