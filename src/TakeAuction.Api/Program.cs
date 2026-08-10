using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Serilog;
using TakeAuction.Api.Common.Api;
using TakeAuction.Api.Common.Caching;
using TakeAuction.Api.Common.Jobs;
using TakeAuction.Api.Common.Messaging;
using TakeAuction.Api.Common.Observability;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Common.RealTime;
using TakeAuction.Api.Common.Security;
using TakeAuction.Api.Features.Auctions;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseTakeAuctionSerilog();

    builder.Services.AddTakeAuctionForwardedHeaders(builder.Configuration);
    builder.Services.AddTakeAuctionApiVersioning();
    builder.Services.AddTakeAuctionSwagger();
    builder.Services.AddTakeAuctionRateLimiting(builder.Configuration);
    builder.Services.AddTakeAuctionAuthentication(builder.Configuration);
    builder.Services.AddTakeAuctionPersistence(builder.Configuration);
    builder.Services.AddTakeAuctionCaching(builder.Configuration);
    builder.Services.AddTakeAuctionMessaging(Assembly.GetExecutingAssembly());
    builder.Services.AddTakeAuctionMessageBroker(builder.Configuration, Assembly.GetExecutingAssembly());
    builder.Services.AddTakeAuctionRealTime(builder.Configuration);
    builder.Services.AddTakeAuctionJobs(builder.Configuration, Assembly.GetExecutingAssembly());
    builder.Services.AddTakeAuctionEndpoints(Assembly.GetExecutingAssembly());
    builder.Services.AddAuctionsFeature();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<ValidationExceptionHandler>();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseTakeAuctionRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseTakeAuctionSwagger();
        app.UseTakeAuctionJobsDashboard();
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    ApiVersionSet versionSet = app.NewApiVersionSet()
        .HasApiVersion(ApiVersioningExtensions.V1)
        .ReportApiVersions()
        .Build();

    RouteGroupBuilder api = app
        .MapGroup("/api/v{version:apiVersion}")
        .WithApiVersionSet(versionSet);

    api.MapGet("/diagnostics/info", (IWebHostEnvironment environment, HttpContext httpContext) => Results.Ok(new
    {
        service = "TakeAuction.Api",
        apiVersion = httpContext.Features.Get<IApiVersioningFeature>()?.RequestedApiVersion?.ToString() ?? "1.0",
        environment = environment.EnvironmentName,
        clientIp = httpContext.Connection.RemoteIpAddress?.ToString(),
        scheme = httpContext.Request.Scheme,
        timestamp = DateTimeOffset.UtcNow
    }))
    .WithName("DiagnosticsInfo")
    .WithTags("Diagnostics")
    .WithSummary("Returns runtime information and the client IP as resolved behind the reverse proxy.");

    api.MapTakeAuctionEndpoints();

    app.MapTakeAuctionRealTime();
    app.UseTakeAuctionRecurringJobs();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        service = "TakeAuction.Api",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTimeOffset.UtcNow
    }))
    .WithName("HealthCheck")
    .WithTags("Diagnostics")
    .DisableRateLimiting();

    if (app.Environment.IsDevelopment())
    {
        await app.MigrateAndSeedAsync();
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "TakeAuction.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
