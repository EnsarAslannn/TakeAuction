using FluentValidation;

namespace TakeAuction.Api.Features.Auctions.GetAuctionById;

public sealed class GetAuctionByIdValidator : AbstractValidator<GetAuctionByIdQuery>
{
    public GetAuctionByIdValidator()
    {
        RuleFor(query => query.AuctionId).NotEmpty();
    }
}
