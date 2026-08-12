namespace TakeAuction.Api.Features.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public long MaxImageSizeInBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Upload root relative to the content root. Deliberately outside wwwroot: the Web SDK
    /// treats everything under wwwroot as a build-time static asset, and files that appear
    /// or vanish at runtime break the build.
    /// </summary>
    public string StorageRoot { get; set; } = "App_Data/uploads";

    public string RequestPath { get; set; } = "/uploads";

    public string ImageFolder { get; set; } = "auctions";

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/avif"
    ];
}
