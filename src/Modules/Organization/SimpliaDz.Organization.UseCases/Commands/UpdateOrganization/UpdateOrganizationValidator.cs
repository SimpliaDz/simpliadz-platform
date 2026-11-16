using FluentValidation;

namespace SimpliaDz.Organization.UseCases.Commands.UpdateOrganization;

public sealed class UpdateOrganizationValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LegalName).MaximumLength(250);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(254).EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.CurrencyCode)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("CurrencyCode must be a three-letter currency code.")
            .When(x => !string.IsNullOrWhiteSpace(x.CurrencyCode));
        RuleFor(x => x.DefaultLanguage).MaximumLength(20);
        RuleFor(x => x.TimeZoneId).MaximumLength(100);
    }
}
