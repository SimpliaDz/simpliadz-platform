using Microsoft.EntityFrameworkCore;
using Simplia.Sales.Contracts.Loyalty;
using Simplia.Sales.Core.Entities;
using Simplia.Sales.Infrastructure.Data;

namespace Simplia.Sales.Infrastructure.Services;

public sealed class LoyaltySalesReader(SalesDbContext db) : ILoyaltySalesReader
{
    public Task<bool?> IsCustomerActiveAsync(Guid customerId, CancellationToken ct) =>
        db.Customers.AsNoTracking().Where(x => x.Id == customerId)
            .Select(x => (bool?)x.IsActive).SingleOrDefaultAsync(ct);

    public async Task<LoyaltySaleSnapshot?> GetSaleAsync(Guid saleId, CancellationToken ct)
    {
        var sale = await db.Sales.AsNoTracking().Where(x => x.Id == saleId)
            .Select(x => new { x.Id, x.CustomerId, x.CompletedBy, x.Status, x.SaleDate, x.Total })
            .SingleOrDefaultAsync(ct);
        if (sale is null) return null;
        var returns = await db.SaleReturns.AsNoTracking()
            .Where(x => x.OriginalSaleId == saleId)
            .OrderBy(x => x.ReturnDate).ThenBy(x => x.Id)
            .Select(x => new LoyaltyReturnSnapshot(x.Id,
                x.Lines.Sum(line => line.RefundAmount), x.CreatedBy))
            .ToListAsync(ct);
        return new LoyaltySaleSnapshot(sale.Id, sale.CustomerId, sale.CompletedBy,
            sale.Status.ToString(), sale.SaleDate, sale.Total, returns);
    }

    public async Task<IReadOnlyList<Guid>> GetCompletedSaleIdsAsync(
        Guid customerId, CancellationToken ct) =>
        await db.Sales.AsNoTracking().Where(x => x.CustomerId == customerId &&
            (x.Status == SaleStatus.Completed || x.Status == SaleStatus.PartiallyReturned ||
             x.Status == SaleStatus.Returned))
            .OrderBy(x => x.SaleDate).Select(x => x.Id).ToListAsync(ct);
}
