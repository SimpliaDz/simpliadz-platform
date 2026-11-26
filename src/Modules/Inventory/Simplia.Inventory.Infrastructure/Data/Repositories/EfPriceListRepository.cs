using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Inventory.Core.Entities.Catalog;
using Simplia.Inventory.UseCases.Prices.Abstractions;

namespace Simplia.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfPriceListRepository : IPriceListRepository
{
    private readonly InventoryDbContext _db;
    public EfPriceListRepository(InventoryDbContext db) => _db = db;
    public Task<PriceList?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.PriceLists.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct) =>
        await _db.PriceLists.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
    public async Task<IReadOnlyList<PriceList>> GetActiveByTypeAsync(PriceListType type,
        CancellationToken ct) =>
        await _db.PriceLists.Include(x => x.Items)
            .Where(x => x.IsActive && x.Type == type).ToListAsync(ct);
    public Task<bool> ExistsByNameAsync(string name, string currencyCode, CancellationToken ct) =>
        _db.PriceLists.AnyAsync(x => x.Name == name && x.CurrencyCode == currencyCode, ct);
    public Task<bool> VariantExistsAsync(Guid variantId, CancellationToken ct) =>
        _db.ProductVariants.AnyAsync(x => x.Id == variantId, ct);
    public async Task AddAsync(PriceList list, CancellationToken ct) =>
        await _db.PriceLists.AddAsync(list, ct);
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A price list or price entry already exists.");
        }
    }
}
