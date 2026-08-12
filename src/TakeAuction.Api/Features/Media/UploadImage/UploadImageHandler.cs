using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed class UploadImageHandler : IRequestHandler<UploadImageCommand, UploadImageResponse>
{
    private readonly MediaStorage _storage;
    private readonly ILogger<UploadImageHandler> _logger;

    public UploadImageHandler(MediaStorage storage, ILogger<UploadImageHandler> logger)
    {
        _storage = storage;
        _logger = logger;
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

        _storage.EnsureCreated();

        var fileName = $"{Guid.CreateVersion7():n}{format.Extension}";

        await using (var target = File.Create(Path.Combine(_storage.ImageRoot, fileName)))
        {
            command.Content.Position = 0;
            await command.Content.CopyToAsync(target, cancellationToken);
        }

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
