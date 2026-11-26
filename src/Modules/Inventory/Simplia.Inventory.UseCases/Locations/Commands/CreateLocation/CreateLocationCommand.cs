using Mediator;
using Simplia.Inventory.Contracts.Locations;

namespace Simplia.Inventory.UseCases.Locations.Commands.CreateLocation;

public sealed record CreateLocationCommand(
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone) : IRequest<LocationResponse>;
