namespace TakeAuction.Api.Features.Auctions;

public static class AuctionsFeatureExtensions
{
    public static IServiceCollection AddAuctionsFeature(this IServiceCollection services)
    {
        services.AddSingleton<AuctionCache>();

        return services;
    }
}
