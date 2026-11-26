using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Simplia.Accounting.Core;

namespace Simplia.Accounting.Infrastructure.Data.Config;

internal sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> b)
    {
        b.ToTable("ExpenseCategories");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.IsActive).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.IsActive);
    }
}
