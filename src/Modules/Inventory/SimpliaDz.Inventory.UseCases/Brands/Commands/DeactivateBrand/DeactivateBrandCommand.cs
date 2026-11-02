using Mediator;

namespace SimpliaDz.Inventory.UseCases.Brands.Commands.DeactivateBrand;

public sealed record DeactivateBrandCommand(Guid Id) : IRequest;
