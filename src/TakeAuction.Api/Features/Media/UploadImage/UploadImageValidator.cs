using FluentValidation;
using Microsoft.Extensions.Options;

namespace TakeAuction.Api.Features.Media.UploadImage;

public sealed class UploadImageValidator : AbstractValidator<UploadImageCommand>
{
    public UploadImageValidator(IOptions<MediaOptions> options)
    {
        var media = options.Value;

        RuleFor(command => command.SizeInBytes)
            .GreaterThan(0)
            .WithMessage("The uploaded file is empty.")
            .LessThanOrEqualTo(media.MaxImageSizeInBytes)
            .WithMessage($"Image must not be larger than {media.MaxImageSizeInBytes / (1024 * 1024)} MB.");

        RuleFor(command => command.ContentType)
            .NotEmpty()
            .Must(contentType => media.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Image must be one of: {string.Join(", ", media.AllowedContentTypes)}.");

        RuleFor(command => command.FileName)
            .NotEmpty()
            .MaximumLength(260);
    }
}
