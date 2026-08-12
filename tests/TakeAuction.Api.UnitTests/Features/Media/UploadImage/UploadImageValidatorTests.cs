using FluentValidation.Results;
using Microsoft.Extensions.Options;
using TakeAuction.Api.Features.Media;
using TakeAuction.Api.Features.Media.UploadImage;

namespace TakeAuction.Api.UnitTests.Features.Media.UploadImage;

public sealed class UploadImageValidatorTests
{
    private readonly MediaOptions _options = new();
    private readonly UploadImageValidator _validator;

    public UploadImageValidatorTests() => _validator = new UploadImageValidator(Options.Create(_options));

    [Fact]
    public async Task Valid_command_passes()
    {
        var result = await _validator.ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Empty_file_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { SizeInBytes = 0 });

        AssertFailedOn(result, nameof(UploadImageCommand.SizeInBytes));
    }

    [Fact]
    public async Task File_above_the_size_limit_fails()
    {
        var result = await _validator.ValidateAsync(
            Valid() with { SizeInBytes = _options.MaxImageSizeInBytes + 1 });

        AssertFailedOn(result, nameof(UploadImageCommand.SizeInBytes));
    }

    [Fact]
    public async Task File_at_the_size_limit_passes()
    {
        var result = await _validator.ValidateAsync(Valid() with { SizeInBytes = _options.MaxImageSizeInBytes });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("application/octet-stream")]
    [InlineData("text/html")]
    [InlineData("")]
    public async Task Disallowed_content_type_fails(string contentType)
    {
        var result = await _validator.ValidateAsync(Valid() with { ContentType = contentType });

        AssertFailedOn(result, nameof(UploadImageCommand.ContentType));
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/avif")]
    public async Task Allowed_content_types_pass(string contentType)
    {
        var result = await _validator.ValidateAsync(Valid() with { ContentType = contentType });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Missing_file_name_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { FileName = "" });

        AssertFailedOn(result, nameof(UploadImageCommand.FileName));
    }

    private static UploadImageCommand Valid() =>
        new(Stream.Null, "photo.png", "image/png", 128_000);

    private static void AssertFailedOn(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
    }
}
