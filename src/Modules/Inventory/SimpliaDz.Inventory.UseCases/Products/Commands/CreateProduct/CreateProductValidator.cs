using FluentValidation;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Code)
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
