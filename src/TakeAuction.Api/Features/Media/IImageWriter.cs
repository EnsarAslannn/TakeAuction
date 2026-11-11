using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.Features.Media;

public interface IImageWriter
{
    Task StoreAsync(string fileName, ImageFormat format, Stream source, CancellationToken cancellationToken);
}
