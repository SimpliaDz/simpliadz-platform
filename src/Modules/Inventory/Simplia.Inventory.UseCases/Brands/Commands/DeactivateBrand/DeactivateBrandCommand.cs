using Mediator;

namespace Simplia.Inventory.UseCases.Brands.Commands.DeactivateBrand;

public sealed record DeactivateBrandCommand(Guid Id) : IRequest;
