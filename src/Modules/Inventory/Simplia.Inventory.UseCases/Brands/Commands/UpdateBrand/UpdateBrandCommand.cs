using Mediator;
using Simplia.Inventory.Contracts.Brands;

namespace Simplia.Inventory.UseCases.Brands.Commands.UpdateBrand;

public sealed record UpdateBrandCommand(Guid Id, string Name) : IRequest<BrandResponse>;
