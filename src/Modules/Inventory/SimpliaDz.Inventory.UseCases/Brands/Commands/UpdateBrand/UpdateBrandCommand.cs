using Mediator;
using SimpliaDz.Inventory.Contracts.Brands;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.UpdateBrand;

public sealed record UpdateBrandCommand(Guid Id, string Name) : IRequest<BrandResponse>;
