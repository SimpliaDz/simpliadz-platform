using Microsoft.EntityFrameworkCore;
using SimpliaDz.Purchasing.Core.Entities;

namespace SimpliaDz.Purchasing.Infrastructure.Data;

public sealed class PurchasingDbContext : DbContext
{
    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("purchasing");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchasingDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        EnsureImmutableRecords();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureImmutableRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureImmutableRecords();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureImmutableRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureImmutableRecords()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<Supplier>().Any(entry => entry.State == EntityState.Deleted))
            throw new InvalidOperationException("Suppliers must be deactivated, not deleted.");
        if (ChangeTracker.Entries<GoodsReceipt>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<GoodsReceiptLine>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Goods receipts are immutable after posting.");
    }
}
