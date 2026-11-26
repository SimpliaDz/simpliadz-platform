using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Contracts.Brands;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Brands.Abstractions;

namespace Simplia.Inventory.UseCases.Brands.Commands.CreateBrand;

public sealed class CreateBrandHandler : IRequestHandler<CreateBrandCommand, BrandResponse>
{
    private readonly IBrandRepository _brands;
    public CreateBrandHandler(IBrandRepository brands) => _brands = brands;

    public async ValueTask<BrandResponse> Handle(CreateBrandCommand request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await _brands.ExistsByNameAsync(name, null, ct))
            throw new ConflictException($"Brand '{name}' already exists.");

        var brand = new Brand(name);
        await _brands.AddAsync(brand, ct);
        await _brands.SaveChangesAsync(ct);
        return brand.ToResponse();
    }
}
