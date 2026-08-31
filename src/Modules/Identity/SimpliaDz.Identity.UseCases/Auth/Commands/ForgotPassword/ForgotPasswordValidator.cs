using FluentValidation;

namespace SimpliaDz.Identity.UseCases.Auth.Commands.ForgotPassword;

public sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
    }
}
