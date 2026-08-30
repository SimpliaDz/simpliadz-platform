using Mediator;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Contracts.Locations;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;

namespace SimpliaDz.Inventory.UseCases.Locations.Commands.CreateLocation;

public sealed class CreateLocationHandler : IRequestHandler<CreateLocationCommand, LocationResponse>
{
    private readonly ILocationRepository _locations;

    public CreateLocationHandler(ILocationRepository locations) => _locations = locations;

    public async ValueTask<LocationResponse> Handle(CreateLocationCommand request, CancellationToken ct)
    {
        var normalizedCode = NormalizeCode(request.Code);
        if (normalizedCode is not null && await _locations.ExistsByCodeAsync(normalizedCode, null, ct))
            throw new ConflictException($"Location code '{normalizedCode}' already exists.");

        var location = new InventoryLocation(
            request.Name,
            normalizedCode,
            Enum.Parse<LocationType>(request.Type, true),
            request.Address,
            request.Phone);

        await _locations.AddAsync(location, ct);
        await _locations.SaveChangesAsync(ct);

        return location.ToResponse();
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
