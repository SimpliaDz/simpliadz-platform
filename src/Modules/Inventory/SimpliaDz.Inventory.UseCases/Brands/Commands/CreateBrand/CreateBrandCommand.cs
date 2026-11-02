using Mediator;
using SimpliaDz.Inventory.Contracts.Brands;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.CreateBrand;

public sealed record CreateBrandCommand(string Name) : IRequest<BrandResponse>;
