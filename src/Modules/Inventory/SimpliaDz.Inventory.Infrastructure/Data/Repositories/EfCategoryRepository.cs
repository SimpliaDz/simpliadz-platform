using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.UseCases.Categories.Abstractions;

namespace SimpliaDz.Inventory.Infrastructure.Data.Repositories;

internal sealed class EfCategoryRepository : ICategoryRepository
{
    private readonly InventoryDbContext _db;

    public EfCategoryRepository(InventoryDbContext db) => _db = db;

    public Task<ProductCategory?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Categories.Include(category => category.Attributes).FirstOrDefaultAsync(category => category.Id == id, ct);

    public async Task<IReadOnlyList<ProductCategory>> GetAllAsync(CancellationToken ct) =>
        await _db.Categories
            .AsNoTracking()
            .Include(category => category.Attributes)
            .OrderBy(category => category.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductAttribute>> GetAttributesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await _db.ProductAttributes.AsNoTracking().Where(attribute => ids.Contains(attribute.Id)).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        _db.Categories.AnyAsync(category => category.Id == id, ct);

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? parentCategoryId,
        Guid? excludingId,
        CancellationToken ct) =>
        _db.Categories.AnyAsync(
            category =>
                category.Name == name &&
                category.ParentCategoryId == parentCategoryId &&
                (!excludingId.HasValue || category.Id != excludingId.Value),
            ct);

    public async Task<bool> WouldCreateCycleAsync(
        Guid categoryId,
        Guid parentCategoryId,
        CancellationToken ct)
    {
        if (categoryId == parentCategoryId)
            return true;

        var parents = await _db.Categories
            .AsNoTracking()
            .Select(category => new { category.Id, category.ParentCategoryId })
            .ToDictionaryAsync(category => category.Id, category => category.ParentCategoryId, ct);

        var visited = new HashSet<Guid>();
        Guid? currentId = parentCategoryId;

        while (currentId.HasValue)
        {
            if (currentId.Value == categoryId || !visited.Add(currentId.Value))
                return true;

            if (!parents.TryGetValue(currentId.Value, out currentId))
                return false;
        }

        return false;
    }

    public async Task AddAsync(ProductCategory category, CancellationToken ct) =>
        await _db.Categories.AddAsync(category, ct);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new ConflictException("A category with the same name already exists under this parent.");
        }
    }
}
