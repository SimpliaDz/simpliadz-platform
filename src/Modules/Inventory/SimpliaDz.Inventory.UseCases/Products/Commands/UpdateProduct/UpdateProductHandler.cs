using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Products;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.UpdateProduct;

public sealed class UpdateProductHandler : IRequestHandler<UpdateProductCommand, ProductResponse>
{
    private readonly IProductRepository _products;

    public UpdateProductHandler(IProductRepository products) => _products = products;

    public async ValueTask<ProductResponse> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Product", request.Id);

        var normalizedCode = string.IsNullOrWhiteSpace(request.Code)
            ? null
            : request.Code.Trim().ToUpperInvariant();

        if (normalizedCode is not null &&
            await _products.ExistsByCodeAsync(normalizedCode, request.Id, ct))
        {
            throw new ConflictException($"Product code '{normalizedCode}' already exists.");
        }

        await ProductReferenceValidation.EnsureValidAsync(
            _products,
            request.BrandId == product.BrandId ? null : request.BrandId,
            request.CategoryId == product.ProductCategoryId ? null : request.CategoryId,
            ct);


        product.Update(
            request.Name,
            normalizedCode,
            request.Description,
            request.BrandId,
            request.CategoryId);

        await _products.SaveChangesAsync(ct);
        return product.ToResponse();
    }
}
