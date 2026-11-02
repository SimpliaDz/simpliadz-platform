using Mediator;
using SimpliaDz.Inventory.Contracts.Brands;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrands;

public sealed class GetBrandsHandler : IRequestHandler<GetBrandsQuery, IReadOnlyList<BrandResponse>>
{
    private readonly IBrandRepository _brands;
    public GetBrandsHandler(IBrandRepository brands) => _brands = brands;

    public async ValueTask<IReadOnlyList<BrandResponse>> Handle(GetBrandsQuery request, CancellationToken ct) =>
        (await _brands.GetAllAsync(ct)).Select(brand => brand.ToResponse()).ToArray();
}
