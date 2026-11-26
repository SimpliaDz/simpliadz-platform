using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.UseCases.Brands.Abstractions;
namespace Simplia.Inventory.UseCases.Brands.Commands.ActivateBrand;
public sealed class ActivateBrandHandler(IBrandRepository brands) : IRequestHandler<ActivateBrandCommand>
{
    public async ValueTask<Unit> Handle(ActivateBrandCommand request, CancellationToken ct)
    { var brand = await brands.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Brand", request.Id); brand.Activate(); await brands.SaveChangesAsync(ct); return default; }
}
