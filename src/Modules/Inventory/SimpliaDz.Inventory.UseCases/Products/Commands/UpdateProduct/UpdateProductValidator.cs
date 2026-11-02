using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        ProductValidation.ApplyCommonRules(this, x => x.Name, x => x.Code, x => x.Description);
    }
}
