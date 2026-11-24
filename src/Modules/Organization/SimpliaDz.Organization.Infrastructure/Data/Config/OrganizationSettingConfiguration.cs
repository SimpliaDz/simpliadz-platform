using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Organization.Core.Entities;
namespace SimpliaDz.Organization.Infrastructure.Data.Config;
internal sealed class OrganizationSettingConfiguration : IEntityTypeConfiguration<OrganizationSetting>
{
    public void Configure(EntityTypeBuilder<OrganizationSetting> builder)
    {
        builder.ToTable("OrganizationSettings"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(150).IsUnicode(false).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique(); builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ValueType).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion(); builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
