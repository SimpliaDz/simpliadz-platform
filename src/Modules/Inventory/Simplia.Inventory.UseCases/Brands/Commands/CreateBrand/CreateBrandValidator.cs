using FluentValidation;

namespace Simplia.Inventory.UseCases.Brands.Commands.CreateBrand;

public sealed class CreateBrandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandValidator() => BrandNameValidation.Apply(this, command => command.Name);
}
