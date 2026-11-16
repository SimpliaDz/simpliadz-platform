using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Inventory.Core.Entities.Catalog;

namespace SimpliaDz.Inventory.Infrastructure.Data.Config.Catalog;

internal sealed class InventoryDocumentTemplateConfiguration : IEntityTypeConfiguration<InventoryDocumentTemplate>
{
    public void Configure(EntityTypeBuilder<InventoryDocumentTemplate> b)
    {
        b.ToTable("InventoryDocumentTemplates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.Title).HasMaxLength(150).IsRequired();
        b.Property(x => x.Columns).HasMaxLength(150).IsRequired();
        b.Property(x => x.PaperSize).HasMaxLength(2).IsRequired();
        b.Property(x => x.FooterText).HasMaxLength(500);
        b.HasData(
            Seed("11a37a5b-14c9-444b-a858-71dc0cb2fd47", "Standard stock list",
                InventoryDocumentType.StockList, "Inventory stock list", "Product,Sku,Barcode,Quantity"),
            Seed("22a37a5b-14c9-444b-a858-71dc0cb2fd47", "Standard count sheet",
                InventoryDocumentType.CountSheet, "Physical inventory count", "Product,Sku,Barcode,Counted"),
            Seed("33a37a5b-14c9-444b-a858-71dc0cb2fd47", "Standard count differences",
                InventoryDocumentType.CountDifferences, "Inventory count differences", "Product,Sku,Expected,Counted,Difference"));
    }

    private static object Seed(string id, string name, InventoryDocumentType type,
        string title, string columns) => new
    {
        Id = Guid.Parse(id), Name = name, Type = type, Title = title,
        Columns = columns, PaperSize = "A4", Landscape = false,
        FooterText = (string?)null, IsActive = true,
        CreatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero),
        UpdatedAt = new DateTimeOffset(2026, 11, 15, 0, 0, 0, TimeSpan.Zero)
    };
}
