using Mediator;
using Simplia.Inventory.Contracts.Locations;

namespace Simplia.Inventory.UseCases.Locations.Queries.GetLocation;

public sealed record GetLocationQuery(Guid Id) : IRequest<LocationResponse>;
