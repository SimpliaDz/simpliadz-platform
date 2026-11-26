using Mediator;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Contracts.Locations;
using Simplia.Inventory.Core.Entities.Stock;
using Simplia.Inventory.UseCases.Locations.Abstractions;

namespace Simplia.Inventory.UseCases.Locations.Commands.UpdateLocation;

public sealed class UpdateLocationHandler : IRequestHandler<UpdateLocationCommand, LocationResponse>
{
    private readonly ILocationRepository _locations;

    public UpdateLocationHandler(ILocationRepository locations) => _locations = locations;

    public async ValueTask<LocationResponse> Handle(UpdateLocationCommand request, CancellationToken ct)
    {
        var location = await _locations.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("Location", request.Id);

        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode is not null && await _locations.ExistsByCodeAsync(normalizedCode, request.Id, ct))
            throw new ConflictException($"Location code '{normalizedCode}' already exists.");

        location.Update(
            request.Name,
            normalizedCode,
            Enum.Parse<LocationType>(request.Type, true),
            request.Address,
            request.Phone);

        await _locations.SaveChangesAsync(ct);
        return location.ToResponse();
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
