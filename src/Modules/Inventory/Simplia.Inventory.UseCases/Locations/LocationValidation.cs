using System.Linq.Expressions;
using FluentValidation;
using Simplia.Inventory.Core.Entities.Stock;

namespace Simplia.Inventory.UseCases.Locations;

internal static class LocationValidation
{
    public static void ApplyCommonRules<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> name,
        Expression<Func<T, string?>> code,
        Expression<Func<T, string>> type,
        Expression<Func<T, string?>> address,
        Expression<Func<T, string?>> phone)
    {
        validator.RuleFor(name)
            .NotEmpty()
            .MaximumLength(150)
            .WithName("Name");

        validator.RuleFor(code)
            .MaximumLength(50)
            .WithName("Code");

        validator.RuleFor(type)
            .NotEmpty()
            .Must(value => Enum.TryParse<LocationType>(value, true, out var parsed) && Enum.IsDefined(parsed))
            .WithMessage("Type must be either 'Store' or 'Warehouse'.")
            .WithName("Type");

        validator.RuleFor(address)
            .MaximumLength(500)
            .WithName("Address");

        validator.RuleFor(phone)
            .MaximumLength(50)
            .WithName("Phone");
    }
}
