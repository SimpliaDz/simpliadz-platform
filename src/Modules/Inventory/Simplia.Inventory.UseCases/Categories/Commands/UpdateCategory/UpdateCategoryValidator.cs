using FluentValidation;

namespace Simplia.Inventory.UseCases.Categories.Commands.UpdateCategory;

public sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryValidator() => CategoryValidation.ApplyNameRule(this, command => command.Name);
}
