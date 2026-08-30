using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Locations;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Locations.Queries.GetLocation;

public sealed class GetLocationHandler : IRequestHandler<GetLocationQuery, LocationResponse>
{
    private readonly ILocationRepository _locations;

    public GetLocationHandler(ILocationRepository locations) => _locations = locations;

    public async ValueTask<LocationResponse> Handle(GetLocationQuery request, CancellationToken ct)
    {
        var location = await _locations.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Location", request.Id);

        return location.ToResponse();
    }
}
