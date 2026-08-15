using FluentValidation;

namespace TakeAuction.Api.Features.Auctions.CancelAuction;

public sealed class CancelAuctionValidator : AbstractValidator<CancelAuctionCommand>
{
    public CancelAuctionValidator()
    {
        RuleFor(command => command.AuctionId)
            .NotEmpty();

        RuleFor(command => command.SellerId)
            .NotEmpty()
            .WithMessage("Seller could not be resolved from the authenticated principal.");
    }
}
