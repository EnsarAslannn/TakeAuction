using FluentValidation;
using TakeAuction.Api.Domain.Users;

namespace TakeAuction.Api.Features.Auth.Register;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(command => command.DisplayName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(128);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(128)
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("'Password' must contain at least one uppercase letter.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("'Password' must contain at least one lowercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("'Password' must contain at least one digit.");

        RuleFor(command => command.Role)
            .Must(BeSelfServiceRole)
            .WithMessage($"'Role' must be either '{nameof(UserRole.Bidder)}' or '{nameof(UserRole.Seller)}'.");
    }

    private static bool BeSelfServiceRole(string role) =>
        string.Equals(role, nameof(UserRole.Bidder), StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, nameof(UserRole.Seller), StringComparison.OrdinalIgnoreCase);
}
