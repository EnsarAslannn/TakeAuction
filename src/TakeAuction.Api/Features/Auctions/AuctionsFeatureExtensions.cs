using TakeAuction.Api.Features.Auctions.ExpireAuctions;
using TakeAuction.Api.Features.Auctions.OpenAuctions;

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
        services.AddScoped<AuctionOpener>();
        services.AddScoped<ActivateAuctionsJob>();
        services.AddScoped<OpenAuctionJob>();
        services.AddScoped<IAuctionOpenSchedule, AuctionOpenSchedule>();

        return services;
    }
}
