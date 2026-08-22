using FluentValidation;

namespace TakeAuction.Api.Features.Auctions.GetAuctions;

public sealed class GetAuctionsValidator : AbstractValidator<GetAuctionsQuery>
{
    public GetAuctionsValidator()
    {
        RuleFor(query => query.Search)
            .MaximumLength(GetAuctionsQuery.MaxSearchLength)
            .When(query => query.Search is not null);
    }
}
