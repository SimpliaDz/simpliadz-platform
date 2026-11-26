using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Products.Abstractions;

namespace Simplia.Inventory.UseCases.Products.Commands.CreateProduct;

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
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            var exists = await _products.ExistsByCodeAsync(normalizedCode, null, ct);
            if (exists)
                throw new ConflictException($"Product code '{normalizedCode}' already exists.");
        }

        await ProductReferenceValidation.EnsureValidAsync(
            _products,
            request.BrandId,
            request.CategoryId,
            ct);


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
