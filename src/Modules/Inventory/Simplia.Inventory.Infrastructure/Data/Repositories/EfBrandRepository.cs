using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Brands.Abstractions;

namespace Simplia.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfBrandRepository : IBrandRepository
{
    private readonly InventoryDbContext _db;

    public EfBrandRepository(InventoryDbContext db) => _db = db;

    public Task<Brand?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Brands.FirstOrDefaultAsync(brand => brand.Id == id, ct);

    public async Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken ct) =>
        await _db.Brands.AsNoTracking().OrderBy(brand => brand.Name).ToListAsync(ct);

    public Task<bool> ExistsByNameAsync(string name, Guid? excludingId, CancellationToken ct) =>
        _db.Brands.AnyAsync(brand => brand.Name == name &&
            (!excludingId.HasValue || brand.Id != excludingId.Value), ct);

    public async Task AddAsync(Brand brand, CancellationToken ct) =>
        await _db.Brands.AddAsync(brand, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A brand with the same name already exists.");
        }
    }
}
