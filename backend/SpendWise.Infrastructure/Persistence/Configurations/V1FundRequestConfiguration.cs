using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.V1Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1FundRequestConfiguration : IEntityTypeConfiguration<V1FundRequest>
{
    public void Configure(EntityTypeBuilder<V1FundRequest> b)
    {
        b.Property(x => x.Kind).HasMaxLength(20); b.Property(x => x.State).HasMaxLength(50);
        b.Property(x => x.Purpose).HasMaxLength(1000); b.Property(x => x.ReviewReason).HasMaxLength(1000);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.HasIndex(x => new { x.FundId, x.RequestedByUserId, x.IdempotencyKey }).IsUnique();
        b.HasIndex(x => new { x.FundId, x.State, x.CreatedAt });
        b.HasOne<V1Fund>().WithMany().HasForeignKey(x => x.FundId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
