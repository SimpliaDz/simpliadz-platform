using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Organization.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Api.Modules;

/// <summary>Owns the short-lived, exclusive cashier-to-register assignment used by the POS.</summary>
public sealed class PosService(SalesDbContext sales, InventoryDbContext inventory,
    OrganizationDbContext organization) : IPosService
{
    public async Task<PosContextDto> GetContextAsync(Guid userId, CancellationToken ct)
    {
        var work = await ActiveWorkSessionAsync(userId, ct);
        var assignment = work is null ? null : await sales.CashRegisterAssignments.AsNoTracking()
            .Include(x => x.CashRegister).FirstOrDefaultAsync(x => x.UserId == userId &&
                x.ReleasedAt == null, ct);
        return await BuildContextAsync(work, assignment, ct);
    }

    public async Task<PosContextDto> AssignAsync(Guid userId, Guid registerId, CancellationToken ct)
    {
        var work = await ActiveWorkSessionAsync(userId, ct)
            ?? throw new ConflictException("Clock in before selecting a cash register.");
        await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var register = await sales.CashRegisters.FirstOrDefaultAsync(x => x.Id == registerId, ct)
                ?? throw new NotFoundException("Cash register", registerId);
            if (!register.IsActive)
                throw new ConflictException("Cash register is inactive.");
            var cashSession = await sales.CashSessions.FirstOrDefaultAsync(x =>
                x.CashRegisterId == registerId && x.Status == CashSessionStatus.Open, ct);
            if (cashSession is null)
                throw new ConflictException("Open a cash session for this register before using it.");
            var occupied = await sales.CashRegisterAssignments.FirstOrDefaultAsync(x =>
                x.CashRegisterId == registerId && x.ReleasedAt == null, ct);
            if (occupied is not null && occupied.UserId != userId)
                throw new ConflictException("Cash register is currently occupied by another cashier.");
            var current = await sales.CashRegisterAssignments.FirstOrDefaultAsync(x =>
                x.UserId == userId && x.ReleasedAt == null, ct);
            if (current is not null && current.CashRegisterId != registerId)
                current.Release();
            if (current is null)
                sales.CashRegisterAssignments.Add(new CashRegisterAssignment(registerId, cashSession.Id,
                    userId, work.Id));
            await sales.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await tx.RollbackAsync(CancellationToken.None);
            sales.ChangeTracker.Clear();
            throw new ConflictException("Cash register was selected by another cashier. Reload and try again.");
        }
        return await GetContextAsync(userId, ct);
    }

    public async Task ReleaseAsync(Guid userId, CancellationToken ct)
    {
        var assignment = await sales.CashRegisterAssignments.FirstOrDefaultAsync(x =>
            x.UserId == userId && x.ReleasedAt == null, ct);
        if (assignment is null) return;
        assignment.Release();
        await sales.SaveChangesAsync(ct);
    }

    public async Task<(Guid LocationId, Guid CashSessionId, Guid PriceListId)> RequireContextAsync(
        Guid userId, CancellationToken ct)
    {
        var work = await ActiveWorkSessionAsync(userId, ct);
        if (work is null) throw new ConflictException("Clock in before making a sale.");
        var assignment = await sales.CashRegisterAssignments.Include(x => x.CashRegister)
            .Include(x => x.CashSession).FirstOrDefaultAsync(x => x.UserId == userId &&
                x.WorkSessionId == work.Id && x.ReleasedAt == null, ct);
        if (assignment is null || assignment.CashSession.Status != CashSessionStatus.Open)
            throw new ConflictException("Select a register with an open cash session before making a sale.");
        var priceList = await DefaultPriceListAsync(ct);
        return (assignment.CashRegister.LocationId, assignment.CashSessionId, priceList.Id);
    }

    private async Task<PosContextDto> BuildContextAsync(
        SimpliaDz.Organization.Core.Entities.WorkSession? work, CashRegisterAssignment? assignment,
        CancellationToken ct)
    {
        var registers = await sales.CashRegisters.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Code).ToListAsync(ct);
        var sessions = await sales.CashSessions.AsNoTracking().Where(x => x.Status == CashSessionStatus.Open)
            .ToDictionaryAsync(x => x.CashRegisterId, ct);
        var occupants = await sales.CashRegisterAssignments.AsNoTracking().Where(x => x.ReleasedAt == null)
            .ToDictionaryAsync(x => x.CashRegisterId, x => x.UserId, ct);
        var org = await organization.Organizations.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        var priceList = org is null ? null : await inventory.PriceLists.AsNoTracking()
            .Where(x => x.IsActive && x.Type == PriceListType.Retail && x.CurrencyCode == org.CurrencyCode)
            .OrderBy(x => x.Name).FirstOrDefaultAsync(ct);
        return new PosContextDto(work is not null, work?.Id, assignment?.CashRegisterId,
            assignment?.CashRegister.Code, assignment?.CashRegister.Name, assignment?.CashSessionId,
            priceList?.Id, org?.CurrencyCode, registers.Select(register =>
            new PosRegisterDto(register.Id, register.LocationId, register.Code, register.Name,
                sessions.GetValueOrDefault(register.Id)?.Id, occupants.GetValueOrDefault(register.Id),
                !occupants.ContainsKey(register.Id))).ToArray());
    }

    private async Task<SimpliaDz.Organization.Core.Entities.WorkSession?> ActiveWorkSessionAsync(
        Guid userId, CancellationToken ct) =>
        await organization.WorkSessions.AsNoTracking().FirstOrDefaultAsync(x =>
            x.UserId == userId && x.ClockOutAt == null, ct);

    private async Task<PriceList> DefaultPriceListAsync(CancellationToken ct)
    {
        var org = await organization.Organizations.AsNoTracking().OrderBy(x => x.Id).FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("Configure the organization currency before using POS.");
        return await inventory.PriceLists.AsNoTracking().Where(x => x.IsActive &&
            x.Type == PriceListType.Retail && x.CurrencyCode == org.CurrencyCode).OrderBy(x => x.Name)
            .FirstOrDefaultAsync(ct) ?? throw new ConflictException(
                "Create an active Retail price list in the organization currency before using POS.");
    }
}
