using FluentValidation;

namespace TakeAuction.Api.Features.Auctions.PlaceBid;

public sealed class PlaceBidValidator : AbstractValidator<PlaceBidCommand>
{
    public PlaceBidValidator()
    {
        RuleFor(command => command.AuctionId)
            .NotEmpty();

        RuleFor(command => command.BidderId)
            .NotEmpty()
            .WithMessage("Bidder could not be resolved from the authenticated principal.");

        RuleFor(command => command.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(1_000_000_000m)
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage("'Amount' must not have more than 2 decimal places.");
    }
}
