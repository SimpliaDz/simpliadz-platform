using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Accounting.Core;

namespace Simplia.Accounting.Infrastructure.Data.Config;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ToTable("Expenses", table =>
            table.HasCheckConstraint("CK_Expenses_Amount_Positive", "[Amount] > 0"));
        b.HasKey(x => x.Id);
        b.HasOne(x => x.ExpenseCategory).WithMany()
            .HasForeignKey(x => x.ExpenseCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Amount).HasPrecision(18, 4).IsRequired();
        b.Property(x => x.ExpenseDate).HasColumnType("date").IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.CreatedBy).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ExpenseDate, x.Id });
        b.HasIndex(x => new { x.ExpenseCategoryId, x.ExpenseDate });
        b.HasIndex(x => new { x.LocationId, x.ExpenseDate });
    }
}
