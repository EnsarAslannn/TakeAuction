using TakeAuction.Api.Features.Auctions.ExpireAuctions;

namespace TakeAuction.Api.Features.Auctions;

public static class AuctionsFeatureExtensions
{
    public static IServiceCollection AddAuctionsFeature(this IServiceCollection services)
    {
        services.AddSingleton<AuctionCache>();
        services.AddScoped<AuctionCloser>();
        services.AddScoped<ExpireAuctionsJob>();
        services.AddScoped<CloseAuctionJob>();
        services.AddScoped<IAuctionCloseSchedule, AuctionCloseSchedule>();

        return services;
    }
}
