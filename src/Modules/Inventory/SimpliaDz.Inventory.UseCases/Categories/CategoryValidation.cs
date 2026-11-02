using System.Linq.Expressions;
using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Categories;

internal static class CategoryValidation
{
    public static void ApplyNameRule<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name)
    {
        validator.RuleFor(name)
            .NotEmpty()
            .MaximumLength(150)
            .WithName("Name");
    }
}
