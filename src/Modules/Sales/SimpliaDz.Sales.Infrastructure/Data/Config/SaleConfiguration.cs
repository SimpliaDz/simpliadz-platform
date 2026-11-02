using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Sales.Core.Entities;

namespace SimpliaDz.Sales.Infrastructure.Data.Config;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("Sales", table =>
        {
            table.HasCheckConstraint("CK_Sales_Subtotal_NonNegative", "[Subtotal] >= 0");
            table.HasCheckConstraint("CK_Sales_Discount_NonNegative", "[DiscountAmount] >= 0");
            table.HasCheckConstraint("CK_Sales_Total_NonNegative", "[Total] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.SaleNumber).HasMaxLength(40);
        b.HasIndex(x => x.SaleNumber).IsUnique().HasFilter("[SaleNumber] IS NOT NULL");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.SaleDate).IsRequired();
        b.Property(x => x.Subtotal).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.DiscountAmount).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.Total).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.Revision).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne(x => x.Sale).HasForeignKey(x => x.SaleId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.LocationId, x.SaleDate });
        b.HasIndex(x => new { x.CustomerId, x.SaleDate });
        b.HasIndex(x => x.Status);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("SaleLines", table =>
        {
            table.HasCheckConstraint("CK_SaleLines_Quantity_Positive", "[Quantity] > 0");
            table.HasCheckConstraint("CK_SaleLines_Amounts_NonNegative",
                "[UnitPrice] >= 0 AND [UnitCostSnapshot] >= 0 AND [DiscountAmount] >= 0 AND [LineTotal] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Ignore(x => x.GrossAmount);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2).IsRequired();
        b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.DiscountAmount).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.LineTotal).HasPrecision(18, 4).IsRequired();
        b.HasIndex(x => new { x.SaleId, x.ProductVariantId }).IsUnique();
        b.HasIndex(x => x.ProductVariantId);
    }
}
