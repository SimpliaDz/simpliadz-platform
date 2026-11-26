using Mediator;
using Simplia.Inventory.Contracts.Brands;

namespace Simplia.Inventory.UseCases.Brands.Queries.GetBrands;

public sealed record GetBrandsQuery : IRequest<IReadOnlyList<BrandResponse>>;
