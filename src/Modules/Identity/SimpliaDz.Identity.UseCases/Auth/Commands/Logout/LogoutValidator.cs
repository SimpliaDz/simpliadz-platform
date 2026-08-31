using FluentValidation;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.Logout;

public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken)
        .NotEmpty()
        .MaximumLength(512);
}
