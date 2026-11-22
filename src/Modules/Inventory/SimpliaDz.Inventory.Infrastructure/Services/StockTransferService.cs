using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SimpliaDz.BuildingBlocks.Exceptions;
using SimpliaDz.Inventory.Core.Entities.Stock;
using SimpliaDz.Inventory.Core.Exceptions;
using SimpliaDz.Inventory.Infrastructure.Data;
using SimpliaDz.Inventory.UseCases.Services;

namespace SimpliaDz.Inventory.Infrastructure.Services;

internal sealed class StockTransferService(InventoryDbContext db) : IStockTransferService
{
    public async Task<StockTransferDto> CreateDraftAsync(Guid sourceLocationId, Guid destinationLocationId,
        string createdBy, CancellationToken ct)
    {
        var transfer = new StockTransfer(sourceLocationId, destinationLocationId, createdBy);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await EnsureActiveLocationsAsync(sourceLocationId, destinationLocationId, ct);
        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(transfer);
    }

    public async Task<StockTransferDto> AddLineAsync(Guid transferId, Guid productVariantId,
        int quantity, CancellationToken ct)
    {
        if (productVariantId == Guid.Empty)
            throw new ArgumentException("Product variant is required.", nameof(productVariantId));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var transfer = await LoadAsync(transferId, ct);
                EnsureDraft(transfer);
                if (!await db.ProductVariants.AnyAsync(v => v.Id == productVariantId && v.IsActive, ct))
                    throw new NotFoundException("Active product variant", productVariantId);
                if (transfer.Lines.Any(line => line.ProductVariantId == productVariantId))
                    throw new ConflictException("This variant is already in the transfer.");

                var line = transfer.AddLine(productVariantId, quantity);
                db.StockTransferLines.Add(line);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Map(transfer);
            }
            catch (DbUpdateException exception) when (IsUniqueConflict(exception))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                throw new ConflictException("This variant is already in the transfer.");
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }

        throw new ConflictException("The transfer changed concurrently; retry the request.");
    }

    public async Task<StockTransferDto> CompleteAsync(Guid transferId, string completedBy,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var transfer = await LoadAsync(transferId, ct);
                EnsureDraft(transfer);
                if (transfer.Lines.Count == 0)
                    throw new ConflictException("Add at least one line before completing the transfer.");
                await EnsureActiveLocationsAsync(transfer.SourceLocationId, transfer.DestinationLocationId, ct);

                var lines = transfer.Lines.OrderBy(line => line.ProductVariantId).ToList();
                var variantIds = lines.Select(line => line.ProductVariantId).ToArray();
                var activeCount = await db.ProductVariants.CountAsync(v =>
                    variantIds.Contains(v.Id) && v.IsActive, ct);
                if (activeCount != lines.Count)
                    throw new ConflictException("All transfer variants must still be active.");

                var balances = await db.StockBalances
                    .Where(b => variantIds.Contains(b.VariantId) &&
                        (b.LocationId == transfer.SourceLocationId ||
                         b.LocationId == transfer.DestinationLocationId))
                    .ToListAsync(ct);
                var byPair = balances.ToDictionary(b => (b.VariantId, b.LocationId));

                // Validate every source before mutating any tracked balance.
                foreach (var line in lines)
                {
                    if (!byPair.TryGetValue((line.ProductVariantId, transfer.SourceLocationId), out var source) ||
                        source.QuantityOnHand < line.Quantity)
                        throw new InsufficientStockException();
                }

                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    var source = byPair[(line.ProductVariantId, transfer.SourceLocationId)];
                    source.ApplyDelta(-line.Quantity);

                    if (!byPair.TryGetValue((line.ProductVariantId, transfer.DestinationLocationId), out var destination))
                    {
                        destination = new StockBalance(line.ProductVariantId, transfer.DestinationLocationId);
                        db.StockBalances.Add(destination);
                        byPair.Add((line.ProductVariantId, transfer.DestinationLocationId), destination);
                    }
                    destination.ApplyDelta(line.Quantity);

                    db.StockMovements.Add(new StockMovement(line.ProductVariantId,
                        transfer.SourceLocationId, StockMovementType.TransferOut, -line.Quantity,
                        completedBy, reference: transfer.TransferNumber,
                        referenceType: "StockTransfer", referenceId: transfer.Id,
                        sourceLineNumber: index + 1));
                    db.StockMovements.Add(new StockMovement(line.ProductVariantId,
                        transfer.DestinationLocationId, StockMovementType.TransferIn, line.Quantity,
                        completedBy, reference: transfer.TransferNumber,
                        referenceType: "StockTransfer", referenceId: transfer.Id,
                        sourceLineNumber: index + 1));
                }

                transfer.Complete(completedBy);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return Map(transfer);
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }

        throw new ConflictException("The transfer changed concurrently; retry the request.");
    }

    public async Task CancelAsync(Guid transferId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var transfer = await LoadAsync(transferId, ct);
        EnsureDraft(transfer);
        transfer.Cancel();
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw new ConflictException("The transfer changed concurrently; retry the request.");
        }
    }

    public async Task<StockTransferDto> GetByIdAsync(Guid transferId, CancellationToken ct) =>
        Map(await LoadAsync(transferId, ct));

    public async Task<IReadOnlyList<StockTransferDto>> ListAsync(int page, int pageSize,
        CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var transfers = await db.StockTransfers.AsNoTracking().Include(t => t.Lines)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return transfers.Select(Map).ToList();
    }

    private async Task<StockTransfer> LoadAsync(Guid transferId, CancellationToken ct) =>
        await db.StockTransfers.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == transferId, ct)
        ?? throw new NotFoundException("Stock transfer", transferId);

    private async Task EnsureActiveLocationsAsync(Guid sourceId, Guid destinationId,
        CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.Id == sourceId && l.IsActive, ct))
            throw new NotFoundException("Active source location", sourceId);
        if (!await db.Locations.AnyAsync(l => l.Id == destinationId && l.IsActive, ct))
            throw new NotFoundException("Active destination location", destinationId);
    }

    private static void EnsureDraft(StockTransfer transfer)
    {
        if (transfer.Status != StockTransferStatus.Draft)
            throw new ConflictException("Only a draft transfer can be changed or completed.");
    }

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is DbUpdateException { InnerException: SqlException { Number: 1205 or 2601 or 2627 } } ||
        exception is SqlException { Number: 1205 };

    private static StockTransferDto Map(StockTransfer transfer) => new(
        transfer.Id, transfer.TransferNumber, transfer.SourceLocationId,
        transfer.DestinationLocationId, transfer.Status.ToString(), transfer.CreatedAt,
        transfer.CreatedBy, transfer.CompletedAt, transfer.CompletedBy,
        transfer.Lines.OrderBy(line => line.ProductVariantId)
            .Select(line => new StockTransferLineDto(line.Id, line.ProductVariantId, line.Quantity))
            .ToList());
}
