using FluentValidation;

namespace TakeAuction.Api.Features.Auctions.CreateAuction;

public sealed class CreateAuctionValidator : AbstractValidator<CreateAuctionCommand>
{
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromDays(30);
    public static readonly TimeSpan StartTolerance = TimeSpan.FromMinutes(1);

    public CreateAuctionValidator(TimeProvider timeProvider)
    {
        RuleFor(command => command.SellerId)
            .NotEmpty()
            .WithMessage("Seller could not be resolved from the authenticated principal.");

        RuleFor(command => command.Title)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200);

        RuleFor(command => command.Description)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(4000);

        RuleFor(command => command.StartingPrice)
            .GreaterThan(0m)
            .LessThanOrEqualTo(1_000_000_000m)
            .Must(HaveAtMostTwoDecimals)
            .WithMessage("'Starting Price' must not have more than 2 decimal places.");

        RuleFor(command => command.MinimumBidIncrement)
            .GreaterThan(0m)
            .LessThanOrEqualTo(1_000_000m)
            .Must(HaveAtMostTwoDecimals)
            .WithMessage("'Minimum Bid Increment' must not have more than 2 decimal places.");

        RuleFor(command => command.StartsAtUtc)
            .Must(startsAt => startsAt >= timeProvider.GetUtcNow() - StartTolerance)
            .WithMessage("'Starts At Utc' must not be in the past.");

        RuleFor(command => command.EndsAtUtc)
            .GreaterThan(command => command.StartsAtUtc)
            .WithMessage("'Ends At Utc' must be later than 'Starts At Utc'.");

        RuleFor(command => command)
            .Must(command => command.EndsAtUtc - command.StartsAtUtc >= MinimumDuration)
            .WithName(nameof(CreateAuctionCommand.EndsAtUtc))
            .WithMessage($"Auction must run for at least {MinimumDuration.TotalMinutes} minutes.")
            .When(command => command.EndsAtUtc > command.StartsAtUtc);

        RuleFor(command => command)
            .Must(command => command.EndsAtUtc - command.StartsAtUtc <= MaximumDuration)
            .WithName(nameof(CreateAuctionCommand.EndsAtUtc))
            .WithMessage($"Auction must not run for longer than {MaximumDuration.TotalDays} days.")
            .When(command => command.EndsAtUtc > command.StartsAtUtc);
    }

    private static bool HaveAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
}
