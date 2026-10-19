using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1BroadcastConfiguration : IEntityTypeConfiguration<V1Broadcast>
{
    public void Configure(EntityTypeBuilder<V1Broadcast> b)
    {
        b.Property(x => x.EventKey).HasMaxLength(200); b.HasIndex(x => x.EventKey).IsUnique();
        b.Property(x => x.Type).HasMaxLength(40); b.Property(x => x.TargetRole).HasMaxLength(20); b.Property(x => x.State).HasMaxLength(40);
        b.Property(x => x.Title).HasMaxLength(200); b.Property(x => x.Message).HasMaxLength(1000);
        b.HasIndex(x => new { x.State, x.ScheduledFor });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<V1MaintenancePlan>().WithMany().HasForeignKey(x => x.MaintenancePlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
