using Mediator;
using Simplia.Inventory.Contracts.Brands;

namespace Simplia.Inventory.UseCases.Brands.Commands.CreateBrand;

public sealed record CreateBrandCommand(string Name) : IRequest<BrandResponse>;
