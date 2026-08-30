using Mediator;
using SimpliaDz.Inventory.Contracts.Locations;

namespace SimpliaDz.Inventory.UseCases.Locations.Queries.GetLocation;

public sealed record GetLocationQuery(Guid Id) : IRequest<LocationResponse>;
