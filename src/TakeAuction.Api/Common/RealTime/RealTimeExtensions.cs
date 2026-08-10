using StackExchange.Redis;

namespace TakeAuction.Api.Common.RealTime;

public static class RealTimeExtensions
{
    public static IServiceCollection AddTakeAuctionRealTime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(RealTimeOptions.SectionName);

        services.AddOptions<RealTimeOptions>().Bind(section);

        var options = section.Get<RealTimeOptions>() ?? new RealTimeOptions();
        var connectionString = options.RedisConnectionString
            ?? configuration.GetConnectionString("Redis");

        var signalR = services.AddSignalR(hub =>
        {
            hub.KeepAliveInterval = TimeSpan.FromSeconds(options.KeepAliveSeconds);
            hub.ClientTimeoutInterval = TimeSpan.FromSeconds(options.ClientTimeoutSeconds);
        });

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            signalR.AddStackExchangeRedis(connectionString, redis =>
                redis.Configuration.ChannelPrefix = RedisChannel.Literal(options.ChannelPrefix));
        }

        services.AddSingleton<IAuctionNotifier, SignalRAuctionNotifier>();

        return services;
    }

    public static IEndpointRouteBuilder MapTakeAuctionRealTime(this IEndpointRouteBuilder builder)
    {
        builder.MapHub<AuctionHub>(AuctionHub.Route)
            .AllowAnonymous()
            .WithTags("RealTime");

        return builder;
    }
}
