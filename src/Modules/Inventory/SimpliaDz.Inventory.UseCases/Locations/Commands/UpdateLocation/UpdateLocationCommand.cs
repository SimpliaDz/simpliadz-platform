using MediatR;
using SimpliaDz.Inventory.Contracts.Locations;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.UpdateLocation;

public sealed record UpdateLocationCommand(
    Guid Id,
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone) : IRequest<LocationResponse>;
