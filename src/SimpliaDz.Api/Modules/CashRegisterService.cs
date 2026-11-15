using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Api.Modules;

public sealed class CashRegisterService(SalesDbContext sales, InventoryDbContext inventory)
    : ICashRegisterService
{
    public async Task<CashRegisterDto> CreateAsync(Guid locationId, string code,
        string name, CancellationToken ct)
    {
        var register = new CashRegister(locationId, code, name);
        if (!await inventory.Locations.AsNoTracking()
                .AnyAsync(x => x.Id == locationId && x.IsActive, ct))
            throw new NotFoundException("Active location", locationId);
        sales.CashRegisters.Add(register);
        await SaveAsync(ct);
        return Map(register);
    }

    public async Task<CashRegisterDto> UpdateAsync(Guid id, string code,
        string name, CancellationToken ct)
    {
        var register = await LoadAsync(id, ct);
        if (!register.IsActive) throw new ConflictException("Cash register is inactive.");
        register.Update(code, name);
        await SaveAsync(ct);
        return Map(register);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var register = await LoadAsync(id, ct);
        if (await sales.CashSessions.AnyAsync(x => x.CashRegisterId == id &&
                x.Status == CashSessionStatus.Open, ct))
            throw new ConflictException("Close the open cash session before deactivating the register.");
        register.Deactivate();
        await SaveAsync(ct);
    }

    public async Task<IReadOnlyList<CashRegisterDto>> ListAsync(Guid? locationId,
        CancellationToken ct)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("Location ID must be valid.", nameof(locationId));
        var query = sales.CashRegisters.AsNoTracking().AsQueryable();
        if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId.Value);
        return await query.OrderBy(x => x.LocationId).ThenBy(x => x.Code)
            .Select(x => new CashRegisterDto(x.Id, x.LocationId, x.Code,
                x.Name, x.IsActive)).ToListAsync(ct);
    }

    private async Task<CashRegister> LoadAsync(Guid id, CancellationToken ct) =>
        await sales.CashRegisters.FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Cash register", id);

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await sales.SaveChangesAsync(ct); }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            sales.ChangeTracker.Clear();
            throw new ConflictException("Cash register code already exists at this location.");
        }
    }

    private static CashRegisterDto Map(CashRegister x) =>
        new(x.Id, x.LocationId, x.Code, x.Name, x.IsActive);
}
