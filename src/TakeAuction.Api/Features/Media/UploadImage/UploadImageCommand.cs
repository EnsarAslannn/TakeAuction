using MediatR;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed record UploadImageCommand(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeInBytes) : IRequest<UploadImageResponse>;

public sealed record UploadImageResponse(string Url, long SizeInBytes);
