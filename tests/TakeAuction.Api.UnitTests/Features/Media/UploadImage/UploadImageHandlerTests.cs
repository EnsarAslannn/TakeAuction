using FluentValidation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using TakeAuction.Api.Features.Media;
using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.UnitTests.Features.Media.UploadImage;

public sealed class UploadImageHandlerTests : IDisposable
{
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"takeauction-media-{Guid.CreateVersion7():n}");
    private readonly MediaOptions _options = new();
    private readonly UploadImageHandler _handler;

    public UploadImageHandlerTests()
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(_root);

        _handler = new UploadImageHandler(
            new MediaStorage(environment, Options.Create(_options)),
            NullLogger<UploadImageHandler>.Instance);
    }

    [Fact]
    public async Task Png_upload_is_written_under_the_configured_folder()
    {
        var response = await _handler.Handle(Command(PngBytes), CancellationToken.None);

        Assert.StartsWith("/uploads/auctions/", response.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", response.Url, StringComparison.Ordinal);
        Assert.Equal(PngBytes.Length, response.SizeInBytes);
    }

    [Fact]
    public async Task Stored_file_keeps_the_original_bytes()
    {
        var response = await _handler.Handle(Command(PngBytes), CancellationToken.None);

        var storedPath = Path.Combine(
            _root,
            "App_Data",
            response.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(storedPath));
        Assert.Equal(PngBytes, await File.ReadAllBytesAsync(storedPath));
    }

    [Fact]
    public async Task Extension_follows_the_detected_format_not_the_declared_one()
    {
        var response = await _handler.Handle(
            Command(PngBytes, fileName: "photo.jpg", contentType: "image/jpeg"),
            CancellationToken.None);

        Assert.EndsWith(".png", response.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_uploads_of_the_same_file_do_not_collide()
    {
        var first = await _handler.Handle(Command(PngBytes), CancellationToken.None);
        var second = await _handler.Handle(Command(PngBytes), CancellationToken.None);

        Assert.NotEqual(first.Url, second.Url);
    }

    [Fact]
    public async Task Content_that_is_not_an_image_is_rejected()
    {
        var portableExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(Command(portableExecutable), CancellationToken.None));
    }

    [Fact]
    public async Task Rejected_content_leaves_nothing_on_disk()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray();

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(Command(svg), CancellationToken.None));

        var folder = Path.Combine(_root, "App_Data", "uploads", "auctions");

        Assert.True(!Directory.Exists(folder) || Directory.GetFiles(folder).Length == 0);
    }

    private static UploadImageCommand Command(
        byte[] content,
        string fileName = "photo.png",
        string contentType = "image/png") =>
        new(new MemoryStream(content), fileName, contentType, content.Length);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
