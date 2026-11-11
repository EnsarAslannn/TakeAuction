using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Common.Persistence;

namespace TakeAuction.Api.Features.Media;

public static class MediaFeatureExtensions
{
    public static IServiceCollection AddMediaFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaOptions>()
            .Bind(configuration.GetSection(MediaOptions.SectionName));

        services.AddSingleton<MediaStorage>();
        services.AddScoped<PostgresImageWriter>();
        services.AddScoped<IImageWriter>(provider =>
            provider.GetRequiredService<IOptions<MediaOptions>>().Value.StoreInPostgres
                ? provider.GetRequiredService<PostgresImageWriter>()
                : provider.GetRequiredService<MediaStorage>());
        services.AddScoped<PurgeOrphanImages.PurgeOrphanImagesJob>();

        return services;
    }

    public static IApplicationBuilder UseTakeAuctionMedia(this WebApplication app)
    {
        var storage = app.Services.GetRequiredService<MediaStorage>();
        var options = app.Services.GetRequiredService<IOptions<MediaOptions>>().Value;
        if (options.StoreInPostgres)
        {
            app.MapGet($"{storage.RequestPath}/{options.ImageFolder.Trim('/')}/{{fileName}}", GetImageAsync)
                .AllowAnonymous();
            return app;
        }

        storage.EnsureCreated();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storage.Root),
            RequestPath = storage.RequestPath,
            ServeUnknownFileTypes = false
        });

        return app;
    }

    private static async Task<IResult> GetImageAsync(
        string fileName, AppDbContext dbContext, HttpContext context, CancellationToken cancellationToken)
    {
        var image = await dbContext.AuctionImages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.FileName == fileName, cancellationToken);
        if (image is null)
        {
            return Results.NotFound();
        }

        context.Response.Headers.CacheControl = "public, max-age=86400, immutable";
        return Results.File(image.Content, image.ContentType, lastModified: image.CreatedAtUtc);
    }
}
