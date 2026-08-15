using FluentValidation.Results;
using TakeAuction.Api.Domain.Auctions;
using TakeAuction.Api.Features.Auctions.PlaceBid;

namespace TakeAuction.Api.UnitTests.Features.Auctions.PlaceBid;

public sealed class PlaceBidValidatorTests
{
    private readonly PlaceBidValidator _validator = new();

    [Fact]
    public async Task Valid_command_passes()
    {
        var result = await _validator.ValidateAsync(Valid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Missing_auction_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { AuctionId = Guid.Empty });

        AssertFailedOn(result, nameof(PlaceBidCommand.AuctionId));
    }

    [Fact]
    public async Task Missing_bidder_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { BidderId = Guid.Empty });

        AssertFailedOn(result, nameof(PlaceBidCommand.BidderId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_amount_fails(decimal amount)
    {
        var result = await _validator.ValidateAsync(Valid() with { Amount = amount });

        AssertFailedOn(result, nameof(PlaceBidCommand.Amount));
    }

    [Fact]
    public async Task Amount_with_more_than_two_decimals_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { Amount = 100.001m });

        AssertFailedOn(result, nameof(PlaceBidCommand.Amount));
    }

    [Fact]
    public async Task Absurdly_large_amount_fails()
    {
        var result = await _validator.ValidateAsync(Valid() with { Amount = 2_000_000_000m });

        AssertFailedOn(result, nameof(PlaceBidCommand.Amount));
    }

    [Fact]
    public async Task Idempotency_key_longer_than_the_column_fails()
    {
        var result = await _validator.ValidateAsync(
            Valid() with { IdempotencyKey = new string('k', Bid.MaxIdempotencyKeyLength + 1) });

        AssertFailedOn(result, nameof(PlaceBidCommand.IdempotencyKey));
    }

    [Fact]
    public async Task Missing_idempotency_key_passes()
    {
        var result = await _validator.ValidateAsync(Valid() with { IdempotencyKey = null });

        Assert.True(result.IsValid);
    }

    private static PlaceBidCommand Valid() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), 150.00m);

    private static void AssertFailedOn(ValidationResult result, string propertyName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == propertyName);
    }
}
