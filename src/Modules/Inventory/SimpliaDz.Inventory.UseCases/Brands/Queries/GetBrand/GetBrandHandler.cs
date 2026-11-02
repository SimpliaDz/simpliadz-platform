using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Brands;
using SimpliaDz.Inventory.UseCases.Brands.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrand;

public sealed class GetBrandHandler : IRequestHandler<GetBrandQuery, BrandResponse>
{
    private readonly IBrandRepository _brands;
    public GetBrandHandler(IBrandRepository brands) => _brands = brands;

    public async ValueTask<BrandResponse> Handle(GetBrandQuery request, CancellationToken ct) =>
        (await _brands.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Brand", request.Id)).ToResponse();
}
