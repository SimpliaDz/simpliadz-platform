using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Products.Abstractions;

namespace Simplia.Inventory.UseCases.Products.Commands.ActivateProduct;

public sealed class ActivateProductHandler : IRequestHandler<ActivateProductCommand>
{
    private readonly IProductRepository _products;

    public ActivateProductHandler(IProductRepository products) => _products = products;

    public async ValueTask<Unit> Handle(ActivateProductCommand request, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Product", request.Id);
        product.Activate();
        await _products.SaveChangesAsync(ct);
        return default;
    }
}
