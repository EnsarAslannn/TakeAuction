namespace TakeAuction.Api.Common.Caching;

public static class CachingExtensions
{
    public static IServiceCollection AddTakeAuctionCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(CacheOptions.SectionName);

        services.AddOptions<CacheOptions>().Bind(section);

        var options = section.Get<CacheOptions>() ?? new CacheOptions();
        var connectionString = options.RedisConnectionString
            ?? configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = connectionString;
                redis.InstanceName = options.InstanceName;
            });
        }

        services.AddSingleton<ICacheService, DistributedCacheService>();

        return services;
    }
}
