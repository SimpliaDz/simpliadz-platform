using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.CreateBrand;

public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator() => BrandNameValidation.Apply(this, command => command.Name);
}
