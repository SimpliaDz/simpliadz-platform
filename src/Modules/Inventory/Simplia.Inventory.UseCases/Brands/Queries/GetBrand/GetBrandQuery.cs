using Mediator;
using Simplia.Inventory.Contracts.Brands;

namespace Simplia.Inventory.UseCases.Brands.Queries.GetBrand;

public sealed record GetBrandQuery(Guid Id) : IRequest<BrandResponse>;
