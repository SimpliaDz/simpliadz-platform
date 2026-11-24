using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Sales.Core.Entities;
using SimpliaDz.Sales.Infrastructure.Data;
using SimpliaDz.Sales.UseCases.Services;

namespace SimpliaDz.Api.Modules;

public sealed class CashSessionService(SalesDbContext sales, InventoryDbContext inventory)
    : ICashSessionService
{
    public async Task<CashSessionDto> OpenAsync(Guid registerId, decimal openingCash,
        Guid openedBy, CancellationToken ct)
    {
        var session = new CashSession(registerId, openedBy, openingCash);
        await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var register = await sales.CashRegisters.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == registerId, ct)
                ?? throw new NotFoundException("Cash register", registerId);
            if (!register.IsActive || !await inventory.Locations.AsNoTracking()
                    .AnyAsync(x => x.Id == register.LocationId && x.IsActive, ct))
                throw new ConflictException("Register and location must be active.");
            if (await sales.CashSessions.AnyAsync(x => x.CashRegisterId == registerId &&
                    x.Status == CashSessionStatus.Open, ct))
                throw new ConflictException("Cash register already has an open session.");
            sales.CashSessions.Add(session);
            await sales.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(session.Id, ct);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await tx.RollbackAsync(CancellationToken.None);
            sales.ChangeTracker.Clear();
            throw new ConflictException("Cash register already has an open session.");
        }
    }

    public async Task<CashSessionDto> CloseAsync(Guid id, decimal countedCash,
        Guid closedBy, CancellationToken ct)
    {
        await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var session = await LoadAsync(id, ct);
            if (session.Status != CashSessionStatus.Open)
                throw new ConflictException("Cash session is already closed.");
            if (await sales.CashRegisterAssignments.AnyAsync(x => x.CashSessionId == id &&
                    x.ReleasedAt == null, ct))
                throw new ConflictException("Release the active cashier from this register before closing its cash session.");
            var cash = await CashPaymentsAsync(id, ct);
            var movements = await MovementTotalAsync(id, ct);
            var expected = checked(session.OpeningCash + cash + movements);
            session.Close(closedBy, countedCash, expected);
            await sales.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await GetAsync(id, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(CancellationToken.None);
            sales.ChangeTracker.Clear();
            throw new ConflictException("Cash session changed concurrently; reload and retry.");
        }
    }

    public async Task<CashMovementDto> AddMovementAsync(Guid id, decimal amountDelta,
        string reason, Guid createdBy, CancellationToken ct)
    {
        var movement = new CashMovement(id, amountDelta, reason, createdBy);
        await using var tx = await sales.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var session = await LoadAsync(id, ct);
        if (session.Status != CashSessionStatus.Open)
            throw new ConflictException("Only an open cash session accepts cash movements.");
        var expected = checked(session.OpeningCash +
            await CashPaymentsAsync(id, ct) + await MovementTotalAsync(id, ct) + amountDelta);
        if (expected < 0)
            throw new ConflictException("Cash out cannot exceed the expected cash in the register.");
        sales.CashMovements.Add(movement);
        await sales.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Map(movement);
    }

    public async Task<CashSessionDto> GetAsync(Guid id, CancellationToken ct) =>
        await MapAsync(await sales.CashSessions.AsNoTracking()
            .Include(x => x.CashRegister).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Cash session", id), ct);

    public async Task<IReadOnlyList<CashSessionDto>> GetCurrentAsync(Guid locationId,
        CancellationToken ct)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location is required.", nameof(locationId));
        var sessions = await sales.CashSessions.AsNoTracking().Include(x => x.CashRegister)
            .Where(x => x.CashRegister.LocationId == locationId &&
                x.Status == CashSessionStatus.Open)
            .OrderBy(x => x.CashRegister.Code).ToListAsync(ct);
        var results = new List<CashSessionDto>(sessions.Count);
        foreach (var session in sessions) results.Add(await MapAsync(session, ct));
        return results;
    }

    public async Task<IReadOnlyList<CashSessionDto>> ListAsync(Guid? locationId,
        int page, int pageSize, CancellationToken ct)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("Location ID must be valid.", nameof(locationId));
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = sales.CashSessions.AsNoTracking().Include(x => x.CashRegister).AsQueryable();
        if (locationId.HasValue) query = query.Where(x => x.CashRegister.LocationId == locationId.Value);
        var sessions = await query.OrderByDescending(x => x.OpenedAt)
            .ThenByDescending(x => x.Id).Skip((page - 1) * pageSize)
            .Take(pageSize).ToListAsync(ct);
        var results = new List<CashSessionDto>(sessions.Count);
        foreach (var session in sessions) results.Add(await MapAsync(session, ct));
        return results;
    }

    public async Task<IReadOnlyList<CashMovementDto>> GetMovementsAsync(Guid id,
        CancellationToken ct)
    {
        if (!await sales.CashSessions.AnyAsync(x => x.Id == id, ct))
            throw new NotFoundException("Cash session", id);
        return await sales.CashMovements.AsNoTracking()
            .Where(x => x.CashSessionId == id)
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
            .Select(x => new CashMovementDto(x.Id, x.CashSessionId,
                x.AmountDelta, x.Reason, x.CreatedBy, x.OccurredAt)).ToListAsync(ct);
    }

    private async Task<CashSession> LoadAsync(Guid id, CancellationToken ct) =>
        await sales.CashSessions.Include(x => x.CashRegister)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new NotFoundException("Cash session", id);

    private async Task<decimal> CashPaymentsAsync(Guid id, CancellationToken ct) =>
        await sales.Payments.AsNoTracking().Where(x =>
            x.CashSessionId == id && x.PaymentMethod == PaymentMethod.Cash)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

    private async Task<decimal> OtherPaymentsAsync(Guid id, CancellationToken ct) =>
        await sales.Payments.AsNoTracking().Where(x =>
            x.CashSessionId == id && x.PaymentMethod != PaymentMethod.Cash)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

    private async Task<decimal> MovementTotalAsync(Guid id, CancellationToken ct) =>
        await sales.CashMovements.AsNoTracking().Where(x => x.CashSessionId == id)
            .SumAsync(x => (decimal?)x.AmountDelta, ct) ?? 0m;

    private async Task<CashSessionDto> MapAsync(CashSession x, CancellationToken ct)
    {
        var cash = await CashPaymentsAsync(x.Id, ct);
        var other = await OtherPaymentsAsync(x.Id, ct);
        var movements = await MovementTotalAsync(x.Id, ct);
        var expected = x.ExpectedCash ?? checked(x.OpeningCash + cash + movements);
        return new CashSessionDto(x.Id, x.CashRegisterId, x.CashRegister.LocationId,
            x.Status.ToString(), x.OpenedBy, x.OpenedAt, x.OpeningCash,
            cash, other, movements, expected, x.CountedCash,
            x.Difference, x.ClosedBy, x.ClosedAt);
    }

    private static CashMovementDto Map(CashMovement x) =>
        new(x.Id, x.CashSessionId, x.AmountDelta, x.Reason, x.CreatedBy, x.OccurredAt);
}
