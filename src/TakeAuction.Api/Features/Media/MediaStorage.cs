using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Features.Media;

public sealed class MediaStorage
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
}
