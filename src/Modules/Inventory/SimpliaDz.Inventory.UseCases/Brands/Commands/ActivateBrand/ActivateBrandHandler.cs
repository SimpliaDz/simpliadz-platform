using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;
namespace SimpliaDz.Inventory.UseCases.Brands.Commands.ActivateBrand;
public sealed class ActivateBrandHandler(IBrandRepository brands) : IRequestHandler<ActivateBrandCommand>
{
    public async ValueTask<Unit> Handle(ActivateBrandCommand request, CancellationToken ct)
    { var brand = await brands.GetByIdAsync(request.Id, ct) ?? throw new NotFoundException("Brand", request.Id); brand.Activate(); await brands.SaveChangesAsync(ct); return default; }
}
