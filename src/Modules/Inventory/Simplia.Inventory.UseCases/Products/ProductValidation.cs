using System.Linq.Expressions;
using FluentValidation;

namespace Simplia.Inventory.UseCases.Products;

internal static class ProductValidation
{
    public static void ApplyCommonRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string?>> code,
        Expression<Func<T, string?>> description)
    {
        validator.RuleFor(name)
            .NotEmpty()
            .MaximumLength(200)
            .WithName("Name");

        validator.RuleFor(code)
            .MaximumLength(50)
            .WithName("Code");

        validator.RuleFor(description)
            .MaximumLength(2000)
            .WithName("Description");
    }
}
