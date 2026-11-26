using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Products.Abstractions;

namespace Simplia.Inventory.UseCases.Products.Commands.DeactivateProduct;

public sealed class DeactivateProductHandler : IRequestHandler<DeactivateProductCommand>
{
    private readonly IProductRepository _products;

    public DeactivateProductHandler(IProductRepository products) => _products = products;

    public async ValueTask<Unit> Handle(DeactivateProductCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Product", request.Id);

        product.Deactivate();
        await _products.SaveChangesAsync(ct);
        return default;
    }
}
