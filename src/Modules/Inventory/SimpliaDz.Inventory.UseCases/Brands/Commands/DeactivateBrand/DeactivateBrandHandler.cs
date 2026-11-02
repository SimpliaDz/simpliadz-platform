using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.DeactivateBrand;

public sealed class DeactivateBrandHandler : IRequestHandler<DeactivateBrandCommand>
{
    private readonly IBrandRepository _brands;
    public DeactivateBrandHandler(IBrandRepository brands) => _brands = brands;

    public async ValueTask<Unit> Handle(DeactivateBrandCommand request, CancellationToken ct)
    {
        var brand = await _brands.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Brand", request.Id);
        brand.Deactivate();
        await _brands.SaveChangesAsync(ct);
        return default;
    }
}
