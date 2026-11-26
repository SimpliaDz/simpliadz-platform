using FluentValidation;

namespace Simplia.Inventory.UseCases.Products.Commands.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        ProductValidation.ApplyCommonRules(this, x => x.Name, x => x.Code, x => x.Description);
    }
}
