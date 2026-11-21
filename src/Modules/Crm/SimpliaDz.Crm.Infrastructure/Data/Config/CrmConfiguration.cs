using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimpliaDz.Crm.Core.Entities;

namespace SimpliaDz.Crm.Infrastructure.Data.Config;

internal sealed class LoyaltyProgramConfiguration : IEntityTypeConfiguration<LoyaltyProgram>
{
    public void Configure(EntityTypeBuilder<LoyaltyProgram> b)
    {
        b.ToTable("LoyaltyPrograms", t =>
        {
            t.HasCheckConstraint("CK_LoyaltyProgram_Scope", "[ScopeKey] = 1");
            t.HasCheckConstraint("CK_LoyaltyProgram_Rate", "[SpendAmountPerPoint] > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        b.Property(x => x.SpendAmountPerPoint).HasPrecision(18, 4);
        b.HasIndex(x => x.ScopeKey).IsUnique();
    }
}

internal sealed class LoyaltyCardConfiguration : IEntityTypeConfiguration<LoyaltyCard>
{
    public void Configure(EntityTypeBuilder<LoyaltyCard> b)
    {
        b.ToTable("LoyaltyCards");
        b.HasKey(x => x.Id);
        b.Property(x => x.CardNumber).HasMaxLength(35).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.CustomerId).IsUnique();
        b.HasIndex(x => x.CardNumber).IsUnique();
    }
}

internal sealed class LoyaltyRewardConfiguration : IEntityTypeConfiguration<LoyaltyReward>
{
    public void Configure(EntityTypeBuilder<LoyaltyReward> b)
    {
        b.ToTable("LoyaltyRewards", t =>
            t.HasCheckConstraint("CK_LoyaltyReward_Cost", "[PointsCost] > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
    }
}

internal sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> b)
    {
        b.ToTable("LoyaltyTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();
        b.Property(x => x.SourceType).HasMaxLength(30).IsRequired();
        b.Property(x => x.SpendAmountPerPointSnapshot).HasPrecision(18, 4);
        b.Property(x => x.RewardNameSnapshot).HasMaxLength(120);
        b.HasOne<LoyaltyCard>().WithMany().HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique();
        b.HasIndex(x => new { x.CardId, x.OccurredAt });
    }
}
