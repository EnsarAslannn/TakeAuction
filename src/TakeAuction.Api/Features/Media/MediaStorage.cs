using Microsoft.Extensions.Options;
using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.Features.Media;

public sealed class MediaStorage : IImageWriter
{
    private readonly MediaOptions _options;

    public MediaStorage(IHostEnvironment environment, IOptions<MediaOptions> options)
    {
        _options = options.Value;

        Root = Path.Combine(
            environment.ContentRootPath,
            _options.StorageRoot.Replace('/', Path.DirectorySeparatorChar));

        ImageRoot = Path.Combine(Root, _options.ImageFolder);
    }

    public string Root { get; }

    public string ImageRoot { get; }

    public string RequestPath => $"/{_options.RequestPath.Trim('/')}";

    public string UrlFor(string fileName) =>
        $"{RequestPath}/{_options.ImageFolder.Trim('/')}/{fileName}";

    public void EnsureCreated() => Directory.CreateDirectory(ImageRoot);

    public async Task StoreAsync(string fileName, ImageFormat format, Stream source, CancellationToken cancellationToken)
    {
        EnsureCreated();
        await using var target = File.Create(Path.Combine(ImageRoot, fileName));
        await source.CopyToAsync(target, cancellationToken);
    }
}
