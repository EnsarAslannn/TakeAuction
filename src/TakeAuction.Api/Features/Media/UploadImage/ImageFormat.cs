using System.Text;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed record ImageFormat(string ContentType, string Extension)
{
    public static readonly ImageFormat Jpeg = new("image/jpeg", ".jpg");
    public static readonly ImageFormat Png = new("image/png", ".png");
    public static readonly ImageFormat WebP = new("image/webp", ".webp");
    public static readonly ImageFormat Avif = new("image/avif", ".avif");

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Identifies the image by its magic bytes rather than the client-supplied content type,
    /// so a renamed executable cannot be stored under an image extension.
    /// </summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> signature)
    {
        if (signature.Length >= 3 && signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF)
        {
            return Jpeg;
        }

        if (signature.Length >= 8 && signature[..8].SequenceEqual(PngSignature))
        {
            return Png;
        }

        if (signature.Length >= 12
            && Ascii(signature[..4]) == "RIFF"
            && Ascii(signature[8..12]) == "WEBP")
        {
            return WebP;
        }

        if (signature.Length >= 12 && Ascii(signature[4..8]) == "ftyp")
        {
            var brand = Ascii(signature[8..12]);
            if (brand is "avif" or "avis")
            {
                return Avif;
            }
        }

        return null;
    }

    private static string Ascii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes);
}
