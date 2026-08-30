using Mediator;
using SimpliaDz.Inventory.Contracts.Locations;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Locations.Queries.GetLocations;

public sealed class GetLocationsHandler : IRequestHandler<GetLocationsQuery, IReadOnlyList<LocationResponse>>
{
    private readonly ILocationRepository _locations;

    public GetLocationsHandler(ILocationRepository locations) => _locations = locations;

    public async ValueTask<IReadOnlyList<LocationResponse>> Handle(GetLocationsQuery request, CancellationToken ct) =>
        (await _locations.GetAllAsync(ct)).Select(location => location.ToResponse()).ToArray();
}
