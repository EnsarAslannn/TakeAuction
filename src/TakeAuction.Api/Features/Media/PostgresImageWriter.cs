using TakeAuction.Api.Common.Persistence;
using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.Features.Media;

public sealed class PostgresImageWriter(AppDbContext dbContext, TimeProvider clock) : IImageWriter
{
    public async Task StoreAsync(string fileName, ImageFormat format, Stream source, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        dbContext.AuctionImages.Add(new(fileName, format.ContentType, buffer.ToArray(), clock.GetUtcNow()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
