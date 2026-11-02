using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Categories.Commands.CreateCategory;

public sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryValidator() => CategoryValidation.ApplyNameRule(this, command => command.Name);
}
