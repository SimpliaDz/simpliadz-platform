using MediatR;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.DeactivateLocation;

public sealed record DeactivateLocationCommand(Guid Id) : IRequest;
