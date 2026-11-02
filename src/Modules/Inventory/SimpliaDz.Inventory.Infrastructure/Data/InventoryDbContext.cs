using Microsoft.EntityFrameworkCore;
using SimpliaDz.Inventory.Core.Entities.Catalog;
using SimpliaDz.Inventory.Core.Entities.Stock;

namespace SimpliaDz.Inventory.Infrastructure.Data;

public sealed class InventoryDbContext : DbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    public DbSet<VariantAttributeValue> VariantAttributeValues => Set<VariantAttributeValue>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
    public DbSet<InventoryLocation> Locations => Set<InventoryLocation>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");

        // Apply ONLY configurations from this module assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        EnsureLedgerIntegrity();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureLedgerIntegrity();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureLedgerIntegrity();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureLedgerIntegrity();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureLedgerIntegrity()
    {
        ChangeTracker.DetectChanges();
        if (ChangeTracker.Entries<StockMovement>().Any(entry =>
            entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock movements are append-only.");

        var movements = ChangeTracker.Entries<StockMovement>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var entry in ChangeTracker.Entries<StockBalance>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Stock balances cannot be deleted.");
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            var before = entry.State == EntityState.Added
                ? 0
                : entry.OriginalValues.GetValue<int>(nameof(StockBalance.QuantityOnHand));
            var delta = (long)entry.Entity.QuantityOnHand - before;
            if (delta == 0 && entry.State == EntityState.Added)
            {
                var zeroOpening = movements.Count(m =>
                    m.VariantId == entry.Entity.VariantId &&
                    m.LocationId == entry.Entity.LocationId &&
                    m.MovementType == StockMovementType.OpeningBalance &&
                    m.QuantityDelta == 0);
                if (zeroOpening != 1)
                    throw new InvalidOperationException("A zero stock balance requires an opening movement.");
                continue;
            }

            var movementDelta = movements
                .Where(m => m.VariantId == entry.Entity.VariantId && m.LocationId == entry.Entity.LocationId)
                .Sum(m => (long)m.QuantityDelta);
            if (delta == 0 || movementDelta != delta)
                throw new InvalidOperationException("A stock balance change requires matching stock movements.");
        }
    }
}
