using System.Text;
using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.UnitTests.Features.Media.UploadImage;

public sealed class ImageFormatTests
{
    [Fact]
    public void Jpeg_signature_is_detected()
    {
        var format = ImageFormat.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        Assert.Equal(ImageFormat.Jpeg, format);
    }

    [Fact]
    public void Png_signature_is_detected()
    {
        var format = ImageFormat.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]);

        Assert.Equal(ImageFormat.Png, format);
    }

    [Fact]
    public void WebP_signature_is_detected()
    {
        var signature = Encoding.ASCII.GetBytes("RIFF").Concat(new byte[] { 0x24, 0x00, 0x00, 0x00 })
            .Concat(Encoding.ASCII.GetBytes("WEBP"))
            .ToArray();

        Assert.Equal(ImageFormat.WebP, ImageFormat.Detect(signature));
    }

    [Fact]
    public void Avif_signature_is_detected()
    {
        var signature = new byte[] { 0x00, 0x00, 0x00, 0x20 }
            .Concat(Encoding.ASCII.GetBytes("ftyp"))
            .Concat(Encoding.ASCII.GetBytes("avif"))
            .ToArray();

        Assert.Equal(ImageFormat.Avif, ImageFormat.Detect(signature));
    }

    [Fact]
    public void Executable_disguised_as_an_image_is_rejected()
    {
        var portableExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00 };

        Assert.Null(ImageFormat.Detect(portableExecutable));
    }

    [Fact]
    public void Svg_markup_is_rejected()
    {
        Assert.Null(ImageFormat.Detect(Encoding.ASCII.GetBytes("<svg xmlns")));
    }

    [Fact]
    public void Truncated_content_is_rejected()
    {
        Assert.Null(ImageFormat.Detect([0xFF, 0xD8]));
    }

    [Fact]
    public void Empty_content_is_rejected()
    {
        Assert.Null(ImageFormat.Detect([]));
    }
}
