using Mediator;

namespace Simplia.Inventory.UseCases.Locations.Commands.DeactivateLocation;

public sealed record DeactivateLocationCommand(Guid Id) : IRequest;
