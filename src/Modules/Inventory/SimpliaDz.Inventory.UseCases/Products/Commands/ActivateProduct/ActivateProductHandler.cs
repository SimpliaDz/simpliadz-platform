using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.UseCases.Products.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Products.Commands.ActivateProduct;

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
