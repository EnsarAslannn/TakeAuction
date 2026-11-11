using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Messaging.Outbox;
using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.OpenAuctions;
using TakeAuction.Api.Features.Auth.PurgeExpiredRefreshTokens;
using TakeAuction.Api.Features.Media.PurgeOrphanImages;
using TakeAuction.Api.Features.Watchlist.RemindWatchers;

namespace TakeAuction.Api.Common.Jobs;

public static class DurableJobsExtensions
{
    public static IServiceCollection AddTakeAuctionDurableJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DurableJobsOptions>()
            .Bind(configuration.GetSection(DurableJobsOptions.SectionName))
            .Validate(options => !options.Enabled ||
                (options.SchedulerUrl is { IsAbsoluteUri: true } uri &&
                 (uri.Scheme == Uri.UriSchemeHttps || (uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)) &&
                 string.IsNullOrEmpty(uri.UserInfo) && options.Secret.Length >= 32),
                "Durable jobs require an HTTPS scheduler URL and a secret of at least 32 characters.")
            .ValidateOnStart();
        services.AddHttpClient<DurableAuctionScheduler>(client => client.Timeout = TimeSpan.FromSeconds(8))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<PurgeExpiredRefreshTokensJob>();
        services.AddScoped<PurgeProcessedOutboxJob>();
        return services;
    }

    public static void MapTakeAuctionDurableJobs(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DurableJobsOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        var expected = SHA256.HashData(Encoding.UTF8.GetBytes($"Bearer {options.Secret}"));
        app.MapPost("/internal/durable-jobs", async (DurableJobRequest request, IServiceProvider services, CancellationToken cancellationToken) =>
        {
            IResult response = Results.NoContent();
            switch (request.Action)
            {
                case "open" when request.AuctionId is { } openId:
                    await services.GetRequiredService<OpenAuctionJob>().RunAsync(openId, cancellationToken);
                    break;
                case "close" when request.AuctionId is { } closeId:
                    await services.GetRequiredService<CloseAuctionJob>().RunAsync(closeId, cancellationToken);
                    break;
                case "remind":
                    await services.GetRequiredService<RemindWatchersJob>().RunAsync(cancellationToken);
                    break;
                case "sweep":
                    await services.GetRequiredService<ExpireAuctionsJob>().RunAsync(cancellationToken);
                    await services.GetRequiredService<ActivateAuctionsJob>().RunAsync(cancellationToken);
                    await services.GetRequiredService<RemindWatchersJob>().RunAsync(cancellationToken);
                    response = Results.Ok(await ReadUpcomingAsync(
                        services.GetRequiredService<AppDbContext>(),
                        services.GetRequiredService<TimeProvider>().GetUtcNow(), cancellationToken));
                    break;
                case "maintenance":
                    await services.GetRequiredService<PurgeExpiredRefreshTokensJob>().RunAsync(cancellationToken);
                    await services.GetRequiredService<PurgeProcessedOutboxJob>().RunAsync(cancellationToken);
                    await services.GetRequiredService<PurgeOrphanImagesJob>().RunAsync(cancellationToken);
                    break;
                case "outbox":
                    break;
                default:
                    return Results.BadRequest();
            }

            var dispatcher = services.GetRequiredService<OutboxDispatcher>();
            for (var batch = 0; batch < 10; batch++)
            {
                var result = await dispatcher.DispatchBatchAsync(cancellationToken);
                if (!result.MoreLikely) break;
            }
            return response;
        })
        .AllowAnonymous()
        .AddEndpointFilter(async (context, next) =>
        {
            var actual = SHA256.HashData(Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers.Authorization.ToString()));
            return CryptographicOperations.FixedTimeEquals(expected, actual)
                ? await next(context)
                : Results.Unauthorized();
        });
    }

    public sealed record DurableJobRequest(string Action, Guid? AuctionId);

    private static async Task<AuctionTimer[]> ReadUpcomingAsync(
        AppDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var horizon = now.AddMinutes(45);
        var opens = await db.Auctions.AsNoTracking()
            .Where(auction => auction.Status == AuctionStatus.Scheduled &&
                auction.StartsAtUtc > now && auction.StartsAtUtc <= horizon)
            .OrderBy(auction => auction.StartsAtUtc)
            .Take(200)
            .Select(auction => new AuctionTimer("open", auction.Id, auction.StartsAtUtc))
            .ToListAsync(cancellationToken);
        var closes = await db.Auctions.AsNoTracking()
            .Where(auction => (auction.Status == AuctionStatus.Scheduled || auction.Status == AuctionStatus.Active) &&
                auction.EndsAtUtc > now && auction.EndsAtUtc <= horizon)
            .OrderBy(auction => auction.EndsAtUtc)
            .Take(200)
            .Select(auction => new AuctionTimer("close", auction.Id, auction.EndsAtUtc))
            .ToListAsync(cancellationToken);
        return [.. opens, .. closes];
    }

    private sealed record AuctionTimer(string Action, Guid AuctionId, DateTimeOffset DueAtUtc);
}
