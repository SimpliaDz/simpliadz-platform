using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using Simplia.Sales.UseCases.Services;

namespace Simplia.Sales.Infrastructure.Services;

public sealed class CustomerService(SalesDbContext db) : ICustomerService
{
    public async Task<CustomerDto> CreateAsync(string name, string? phone, string? email,
        string? address, CustomerType customerType, string? notes, CancellationToken ct)
    {
        var customer = new Customer(name, phone, email, address, customerType, notes);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return Map(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, string name, string? phone,
        string? email, string? address, CustomerType customerType, string? notes, CancellationToken ct)
    {
        var customer = await LoadAsync(id, ct);
        customer.Update(name, phone, email, address, customerType, notes);
        await SaveAsync(ct);
        return Map(customer);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var customer = await LoadAsync(id, ct);
        customer.Deactivate();
        await SaveAsync(ct);
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        Map(await LoadAsync(id, ct));

    public async Task<IReadOnlyList<CustomerDto>> SearchAsync(string? search,
        CustomerType? customerType, bool? isActive, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = db.Customers.AsNoTracking().AsQueryable();
        if (customerType.HasValue) query = query.Where(x => x.CustomerType == customerType.Value);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => x.Name.ToLower().Contains(term) ||
                (x.Phone != null && x.Phone.ToLower().Contains(term)) ||
                (x.Email != null && x.Email.ToLower().Contains(term)));
        }
        var customers = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return customers.Select(Map).ToList();
    }

    private async Task<Customer> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Customer", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Customer changed concurrently; reload and retry.");
        }
    }

    private static CustomerDto Map(Customer x) => new(x.Id, x.Name, x.Phone, x.Email,
        x.Address, x.CustomerType.ToString(), x.Notes, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
