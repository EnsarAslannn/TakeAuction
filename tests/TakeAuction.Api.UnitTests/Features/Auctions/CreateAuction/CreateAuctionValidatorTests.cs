using FluentValidation.Results;
using TakeAuction.Api.Features.Auctions.CreateAuction;
using TakeAuction.Api.UnitTests.Common;

namespace TakeAuction.Api.UnitTests.Features.Auctions.CreateAuction;

public sealed class CreateAuctionValidatorTests
{
    private readonly FixedTimeProvider _timeProvider = new(TestHarness.Now);
    private readonly CreateAuctionValidator _validator;

    public CreateAuctionValidatorTests() => _validator = new CreateAuctionValidator(_timeProvider);

    [Fact]
    public async Task Valid_command_passes()
    {
        var result = await _validator.ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Missing_seller_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { SellerId = Guid.Empty });

        AssertFailedOn(result, nameof(CreateAuctionCommand.SellerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task Short_or_empty_title_fails(string title)
    {
        var result = await _validator.ValidateAsync(Valid() with { Title = title });

        AssertFailedOn(result, nameof(CreateAuctionCommand.Title));
    }

    [Fact]
    public async Task Title_over_200_characters_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { Title = new string('a', 201) });

        AssertFailedOn(result, nameof(CreateAuctionCommand.Title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too short")]
    public async Task Short_or_empty_description_fails(string description)
    {
        var result = await _validator.ValidateAsync(Valid() with { Description = description });

        AssertFailedOn(result, nameof(CreateAuctionCommand.Description));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_starting_price_fails(decimal startingPrice)
    {
        var result = await _validator.ValidateAsync(Valid() with { StartingPrice = startingPrice });

        AssertFailedOn(result, nameof(CreateAuctionCommand.StartingPrice));
    }

    [Fact]
    public async Task Starting_price_with_more_than_two_decimals_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { StartingPrice = 10.005m });

        AssertFailedOn(result, nameof(CreateAuctionCommand.StartingPrice));
    }

    [Fact]
    public async Task Non_positive_minimum_bid_increment_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { MinimumBidIncrement = 0m });

        AssertFailedOn(result, nameof(CreateAuctionCommand.MinimumBidIncrement));
    }

    [Fact]
    public async Task Start_in_the_past_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            StartsAtUtc = TestHarness.Now.AddHours(-2),
            EndsAtUtc = TestHarness.Now.AddHours(-1)
        });

        AssertFailedOn(result, nameof(CreateAuctionCommand.StartsAtUtc));
    }

    [Fact]
    public async Task End_before_start_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            StartsAtUtc = TestHarness.Now.AddHours(2),
            EndsAtUtc = TestHarness.Now.AddHours(1)
        });

        AssertFailedOn(result, nameof(CreateAuctionCommand.EndsAtUtc));
    }

    [Fact]
    public async Task Duration_below_the_minimum_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            StartsAtUtc = TestHarness.Now,
            EndsAtUtc = TestHarness.Now.AddMinutes(1)
        });

        AssertFailedOn(result, nameof(CreateAuctionCommand.EndsAtUtc));
    }

    [Fact]
    public async Task Duration_above_the_maximum_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            StartsAtUtc = TestHarness.Now,
            EndsAtUtc = TestHarness.Now.AddDays(31)
        });

        AssertFailedOn(result, nameof(CreateAuctionCommand.EndsAtUtc));
    }

    [Fact]
    public async Task Start_slightly_in_the_past_is_tolerated()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            StartsAtUtc = TestHarness.Now.AddSeconds(-30),
            EndsAtUtc = TestHarness.Now.AddHours(1)
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Missing_image_url_is_allowed()
    {
        var result = await _validator.ValidateAsync(Valid() with { ImageUrl = null });

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Uploaded_image_path_passes()
    {
        var result = await _validator.ValidateAsync(Valid() with
        {
            ImageUrl = "/uploads/auctions/0199ff31170679578e0d09183f6ee566.webp"
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("https://evil.example/pixel.png")]
    [InlineData("/uploads/auctions/../../appsettings.json")]
    [InlineData("/uploads/auctions/not-a-guid.png")]
    [InlineData("/uploads/auctions/0199ff31170679578e0d09183f6ee566.svg")]
    [InlineData("javascript:alert(1)")]
    public async Task Image_url_outside_the_upload_folder_fails(string imageUrl)
    {
        var result = await _validator.ValidateAsync(Valid() with { ImageUrl = imageUrl });

        AssertFailedOn(result, nameof(CreateAuctionCommand.ImageUrl));
    }

    private static CreateAuctionCommand Valid() => new(
        Guid.CreateVersion7(),
        "Vintage mechanical watch",
        "A fully serviced 1968 mechanical watch with original box and papers.",
        1500.00m,
        25.00m,
        TestHarness.Now.AddHours(1),
        TestHarness.Now.AddDays(3));

    private static void AssertFailedOn(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
    }
}
