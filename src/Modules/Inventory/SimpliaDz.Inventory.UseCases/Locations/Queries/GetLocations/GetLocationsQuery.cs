using Mediator;
using SimpliaDz.Inventory.Contracts.Locations;

namespace SimpliaDz.Inventory.UseCases.Locations.Queries.GetLocations;

public sealed record GetLocationsQuery : IRequest<IReadOnlyList<LocationResponse>>;
