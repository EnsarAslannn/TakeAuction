using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed class UploadImageHandler : IRequestHandler<UploadImageCommand, UploadImageResponse>
{
    private readonly MediaStorage _storage;
    private readonly ILogger<UploadImageHandler> _logger;
    private readonly IImageWriter _writer;

    public UploadImageHandler(MediaStorage storage, ILogger<UploadImageHandler> logger, IImageWriter? writer = null)
    {
        _storage = storage;
        _logger = logger;
        _writer = writer ?? storage;
    }

    public async Task<UploadImageResponse> Handle(UploadImageCommand command, CancellationToken cancellationToken)
    {
        var signature = await ReadSignatureAsync(command.Content, cancellationToken);
        var format = ImageFormat.Detect(signature);

        if (format is null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(UploadImageCommand.Content),
                    "File content is not a supported image. Upload a JPEG, PNG, WebP or AVIF file.")
            ]);
        }

        var fileName = $"{Guid.CreateVersion7():n}{format.Extension}";

        command.Content.Position = 0;
        await _writer.StoreAsync(fileName, format, command.Content, cancellationToken);

        var url = _storage.UrlFor(fileName);

        _logger.LogInformation(
            "Stored {Format} image {FileName} ({SizeInBytes} bytes) at {Url}",
            format.Extension,
            command.FileName,
            command.SizeInBytes,
            url);

        return new UploadImageResponse(url, command.SizeInBytes);
    }

    private static async Task<byte[]> ReadSignatureAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;

        var buffer = new byte[12];
        var read = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        return read < buffer.Length ? buffer[..read] : buffer;
    }
}
