using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.UseCases.Locations.Abstractions;

namespace SimpliaDz.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfLocationRepository : ILocationRepository
{
    private readonly InventoryDbContext _db;

    public EfLocationRepository(InventoryDbContext db) => _db = db;

    public Task<InventoryLocation?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Locations.FirstOrDefaultAsync(location => location.Id == id, ct);

    public async Task<IReadOnlyList<InventoryLocation>> GetAllAsync(CancellationToken ct) =>
        await _db.Locations
            .AsNoTracking()
            .OrderBy(location => location.Name)
            .ThenBy(location => location.Code)
            .ToListAsync(ct);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludingId, CancellationToken ct) =>
        _db.Locations.AnyAsync(
            location => location.Code == code && (!excludingId.HasValue || location.Id != excludingId.Value),
            ct);

    public Task<bool> IsActiveAsync(Guid id, CancellationToken ct) =>
        _db.Locations.AnyAsync(location => location.Id == id && location.IsActive, ct);

    public async Task AddAsync(InventoryLocation location, CancellationToken ct) =>
        await _db.Locations.AddAsync(location, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A location with the same code already exists.");
        }
    }
}
