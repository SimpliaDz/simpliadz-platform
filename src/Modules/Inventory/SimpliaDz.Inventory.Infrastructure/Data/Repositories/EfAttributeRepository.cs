using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Attributes.Abstractions;

namespace SimpliaDz.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfAttributeRepository : IAttributeRepository
{
    private readonly InventoryDbContext _db;
    public EfAttributeRepository(InventoryDbContext db) => _db = db;

    public Task<ProductAttribute?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.ProductAttributes.Include(x => x.Values).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ProductAttribute>> GetAllAsync(CancellationToken ct) =>
        await _db.ProductAttributes.AsNoTracking().Include(x => x.Values)
            .OrderBy(x => x.Name).ToListAsync(ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct) =>
        _db.ProductAttributes.AnyAsync(x => x.Name == name, ct);

    public async Task AddAsync(ProductAttribute attribute, CancellationToken ct) =>
        await _db.ProductAttributes.AddAsync(attribute, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("The attribute name or value already exists.");
        }
    }
}
