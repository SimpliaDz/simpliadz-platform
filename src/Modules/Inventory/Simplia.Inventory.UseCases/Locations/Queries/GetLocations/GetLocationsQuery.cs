using Mediator;
using Simplia.Inventory.Contracts.Locations;

namespace Simplia.Inventory.UseCases.Locations.Queries.GetLocations;

public sealed record GetLocationsQuery : IRequest<IReadOnlyList<LocationResponse>>;
