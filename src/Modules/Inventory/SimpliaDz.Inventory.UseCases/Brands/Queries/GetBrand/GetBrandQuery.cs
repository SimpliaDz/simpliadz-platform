using Mediator;
using SimpliaDz.Inventory.Contracts.Brands;

namespace SimpliaDz.Inventory.UseCases.Brands.Queries.GetBrand;

public sealed record GetBrandQuery(Guid Id) : IRequest<BrandResponse>;
