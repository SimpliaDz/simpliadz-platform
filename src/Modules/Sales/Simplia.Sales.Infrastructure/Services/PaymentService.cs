using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Simplia.BuildingBlocks.Exceptions;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;
using Simplia.Sales.UseCases.Services;

namespace Simplia.Sales.Infrastructure.Services;

public sealed class PaymentService(SalesDbContext db) : IPaymentService
{
    public async Task<PaymentDto> RecordAsync(Guid paymentId, Guid saleId, decimal amount,
        PaymentMethod method, Guid receivedBy, CancellationToken ct,
        Guid? cashSessionId = null, string? reference = null)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (await db.Payments.AnyAsync(x => x.Id == paymentId, ct))
                    throw new ConflictException("Payment has already been recorded.");
                var sale = await db.Sales.FirstOrDefaultAsync(x => x.Id == saleId, ct)
                    ?? throw new NotFoundException("Sale", saleId);
                if (sale.Status != SaleStatus.Completed)
                    throw new ConflictException("Only completed sales can receive payments.");
                if (cashSessionId.HasValue)
                {
                    var session = await db.CashSessions.Include(x => x.CashRegister)
                        .FirstOrDefaultAsync(x => x.Id == cashSessionId.Value, ct)
                        ?? throw new NotFoundException("Cash session", cashSessionId.Value);
                    if (session.Status != CashSessionStatus.Open ||
                        session.CashRegister.LocationId != sale.LocationId)
                        throw new ConflictException("Payment requires an open cash session at the sale location.");
                    if (!await db.CashRegisterAssignments.AnyAsync(x => x.UserId == receivedBy &&
                            x.CashSessionId == session.Id && x.ReleasedAt == null, ct))
                        throw new ConflictException("Select your assigned cash register before recording a payment.");
                }
                else if (await db.CashRegisters.AnyAsync(x =>
                             x.LocationId == sale.LocationId && x.IsActive, ct))
                    throw new ConflictException("Select an open cash session for this payment.");
                if (amount > sale.BalanceDue)
                    throw new ConflictException("Payment exceeds the remaining sale total.");
                var payment = new Payment(paymentId, saleId, amount, method, receivedBy, cashSessionId, reference: reference);
                sale.RecordPayment(amount);
                db.Payments.Add(payment);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Map(payment);
            }
            catch (Exception exception) when (attempt < 4 && IsRetryable(exception))
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (IsUniqueConflict(exception))
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw new ConflictException("Payment has already been recorded.");
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
        throw new ConflictException("Payment could not be recorded due to concurrent updates.");
    }

    public async Task<PaymentDto> GetByIdAsync(Guid paymentId, CancellationToken ct) =>
        Map(await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct)
            ?? throw new NotFoundException("Payment", paymentId));

    public async Task<IReadOnlyList<PaymentDto>> ListBySaleAsync(Guid saleId, CancellationToken ct)
    {
        if (!await db.Sales.AnyAsync(x => x.Id == saleId, ct))
            throw new NotFoundException("Sale", saleId);
        var payments = await db.Payments.AsNoTracking().Where(x => x.SaleId == saleId)
            .OrderBy(x => x.PaidAt).ThenBy(x => x.Id).ToListAsync(ct);
        return payments.Select(Map).ToList();
    }

    private static PaymentDto Map(Payment x) => new(x.Id, x.SaleId, x.Amount,
        x.PaymentMethod.ToString(), x.PaidAt, x.ReceivedBy, x.CashSessionId, x.Reference);

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsRetryable(Exception exception) =>
        exception is DbUpdateConcurrencyException ||
        exception is SqlException { Number: 1205 } ||
        exception.InnerException is not null && IsRetryable(exception.InnerException);
}
