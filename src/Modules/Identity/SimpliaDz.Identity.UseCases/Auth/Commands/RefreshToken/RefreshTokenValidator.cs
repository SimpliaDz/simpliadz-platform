using FluentValidation;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.RefreshToken;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken)
        .NotEmpty()
        .MaximumLength(512);
}
