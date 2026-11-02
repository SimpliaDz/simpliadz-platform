using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Brands;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.UpdateBrand;

public sealed class UpdateBrandHandler : IRequestHandler<UpdateBrandCommand, BrandResponse>
{
    private readonly IBrandRepository _brands;
    public UpdateBrandHandler(IBrandRepository brands) => _brands = brands;

    public async ValueTask<BrandResponse> Handle(UpdateBrandCommand request, CancellationToken ct)
    {
        var brand = await _brands.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Brand", request.Id);
        var name = request.Name.Trim();
        if (await _brands.ExistsByNameAsync(name, brand.Id, ct))
            throw new ConflictException($"Brand '{name}' already exists.");

        brand.SetName(name);
        await _brands.SaveChangesAsync(ct);
        return brand.ToResponse();
    }
}
