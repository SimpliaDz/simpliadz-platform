using Mediator;
using SimpliaDz.Inventory.Contracts.Brands;

namespace SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrands;

public sealed record GetBrandsQuery : IRequest<IReadOnlyList<BrandResponse>>;
