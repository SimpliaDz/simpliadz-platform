using Mediator;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.CreateProduct;

public sealed class CreateProductHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IProductRepository _products;

    public CreateProductHandler(IProductRepository products)
    {
        _products = products;
    }

    public async ValueTask<Guid> Handle(CreateProductCommand request, CancellationToken ct)
    {
        // Optional uniqueness rule for Code (if provided)
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var exists = await _products.ExistsByCodeAsync(request.Code.Trim(), ct);
            if (exists)
                throw new InvalidOperationException($"Product code '{request.Code}' already exists.");
        }

        var product = new Product(
            name: request.Name,
            code: request.Code,
            description: request.Description);

        product.AssignBrand(request.BrandId);
        product.AssignCategory(request.CategoryId);

        await _products.AddAsync(product, ct);
        await _products.SaveChangesAsync(ct);

        return product.Id;
    }
}
