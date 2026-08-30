using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.DeactivateLocation;

public sealed class DeactivateLocationHandler : IRequestHandler<DeactivateLocationCommand>
{
    private readonly ILocationRepository _locations;

    public DeactivateLocationHandler(ILocationRepository locations) => _locations = locations;

    public async ValueTask<Unit> Handle(DeactivateLocationCommand request, CancellationToken ct)
    {
        var location = await _locations.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Location", request.Id);

        location.Deactivate();
        await _locations.SaveChangesAsync(ct);
        return default;
    }
}