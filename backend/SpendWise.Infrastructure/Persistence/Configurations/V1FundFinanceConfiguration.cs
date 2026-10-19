using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1FundTreasuryConfiguration : IEntityTypeConfiguration<V1FundTreasury>
{
    public void Configure(EntityTypeBuilder<V1FundTreasury> b)
    {
        b.HasIndex(x => x.FundId).IsUnique(); b.HasIndex(x => x.LedgerAccountId).IsUnique();
        b.Property(x => x.MembershipSignature).HasMaxLength(64);
        b.HasOne<V1Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LedgerAccount>().WithMany().HasForeignKey(x => x.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1FundObligationConfiguration : IEntityTypeConfiguration<V1FundObligation>
{
    public void Configure(EntityTypeBuilder<V1FundObligation> b)
    {
        b.HasIndex(x => new { x.FundId, x.Revision, x.MembershipId }).IsUnique();
        b.HasOne<V1Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<V1FundMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1FundContributionConfiguration : IEntityTypeConfiguration<V1FundContribution>
{
    public void Configure(EntityTypeBuilder<V1FundContribution> b)
    {
        b.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        b.Property(x => x.IdempotencyKey).HasMaxLength(100); b.Property(x => x.Purpose).HasMaxLength(500);
        b.HasOne<V1Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<V1FundMembership>().WithMany().HasForeignKey(x => x.MembershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
