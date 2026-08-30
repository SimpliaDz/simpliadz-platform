using MediatR;
using SimpliaDz.Inventory.Contracts.Locations;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.CreateLocation;

public sealed record CreateLocationCommand(
    string Name,
    string? Code,
    string Type,
    string? Address,
    string? Phone) : IRequest<LocationResponse>;
