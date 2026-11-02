using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Brands;

internal static class BrandNameValidation
{
    public static void Apply<T>(AbstractValidator<T> validator, System.Linq.Expressions.Expression<Func<T, string>> name) =>
        validator.RuleFor(name).NotEmpty().MaximumLength(150).WithName("Name");
}
