using TakeAuction.Api.Features.Watchlist.RemindWatchers;

namespace TakeAuction.Api.Features.Watchlist;

public static class WatchlistFeatureExtensions
{
    public static IServiceCollection AddWatchlistFeature(this IServiceCollection services)
    {
        services.AddScoped<RemindWatchersJob>();

        return services;
    }
}
