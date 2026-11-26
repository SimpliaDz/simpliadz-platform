using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Purchasing.Core.Entities;
using Simplia.Purchasing.Infrastructure.Data;
using Simplia.Purchasing.UseCases.Services;

namespace Simplia.Purchasing.Infrastructure.Services;

internal sealed class SupplierService(PurchasingDbContext db) : ISupplierService
{
    public async Task<SupplierDto> CreateAsync(string name, string? phone, string? email,
        string? address, string? notes, CancellationToken ct)
    {
        var supplier = new Supplier(name, phone, email, address, notes);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return Map(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, string name, string? phone, string? email,
        string? address, string? notes, CancellationToken ct)
    {
        var supplier = await LoadAsync(id, ct);
        supplier.Update(name, phone, email, address, notes);
        await SaveAsync(ct);
        return Map(supplier);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var supplier = await LoadAsync(id, ct);
        supplier.Deactivate();
        await SaveAsync(ct);
    }

    public async Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        Map(await LoadAsync(id, ct));

    public async Task<IReadOnlyList<SupplierDto>> SearchAsync(string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));

        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (isActive.HasValue) query = query.Where(s => s.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(s => s.Name.Contains(term) ||
                (s.Phone != null && s.Phone.Contains(term)) ||
                (s.Email != null && s.Email.Contains(term)));
        }

        return await query.OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SupplierDto(s.Id, s.Name, s.Phone, s.Email,
                s.Address, s.Notes, s.IsActive, s.CreatedAt, s.UpdatedAt))
            .ToListAsync(ct);
    }

    private async Task<Supplier> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("Supplier", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Supplier changed concurrently; reload and retry.");
        }
    }

    private static SupplierDto Map(Supplier supplier) => new(supplier.Id, supplier.Name,
        supplier.Phone, supplier.Email, supplier.Address, supplier.Notes,
        supplier.IsActive, supplier.CreatedAt, supplier.UpdatedAt);
}
