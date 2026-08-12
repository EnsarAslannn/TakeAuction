using Microsoft.Extensions.FileProviders;

namespace TakeAuction.Api.Features.Media;

public static class MediaFeatureExtensions
{
    public static IServiceCollection AddMediaFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.SectionName));

        services.AddSingleton<MediaStorage>();

        return services;
    }

    public static IApplicationBuilder UseTakeAuctionMedia(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<MediaStorage>();
        storage.EnsureCreated();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.Root),
            RequestPath = storage.RequestPath,
            ServeUnknownFileTypes = false
        });

        return app;
    }
}
