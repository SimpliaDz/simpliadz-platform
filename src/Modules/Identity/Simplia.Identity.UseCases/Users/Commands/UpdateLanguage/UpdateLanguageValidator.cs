using FluentValidation;

namespace Simplia.Identity.UseCases.Users.Commands.UpdateLanguage;

public sealed class UpdateLanguageValidator : AbstractValidator<UpdateLanguageCommand>
{
    public UpdateLanguageValidator()
    {
        RuleFor(x => x.LanguageCode)
            .NotEmpty()
            .Must(x => x is not null && new[] { "en", "ar", "fr" }
                .Contains(x.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("LanguageCode must be en, ar, or fr.");
    }
}
