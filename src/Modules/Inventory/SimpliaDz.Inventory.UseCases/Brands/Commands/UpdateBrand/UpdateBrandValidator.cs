using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.UpdateBrand;

public sealed class UpdateBrandValidator : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandValidator() => BrandNameValidation.Apply(this, command => command.Name);
}
