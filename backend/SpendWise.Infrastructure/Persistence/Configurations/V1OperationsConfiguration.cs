using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class V1MaintenanceControlConfiguration : IEntityTypeConfiguration<V1MaintenanceControl>
{
    public void Configure(EntityTypeBuilder<V1MaintenanceControl> b) { b.HasKey(c => c.Id); b.HasData(new V1MaintenanceControl { Id = 1 }); }
}

public sealed class V1UserActivityConfiguration : IEntityTypeConfiguration<V1UserActivity>
{
    public void Configure(EntityTypeBuilder<V1UserActivity> b)
    {
        b.HasIndex(a => a.UserId).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class V1MaintenancePlanConfiguration : IEntityTypeConfiguration<V1MaintenancePlan>
{
    public void Configure(EntityTypeBuilder<V1MaintenancePlan> b)
    {
        b.Property(p => p.Reason).HasMaxLength(1000);
        b.Property(p => p.Scope).HasMaxLength(20).HasDefaultValue("Global");
        b.Property(p => p.OperationKey).HasMaxLength(100); b.Property(p => p.CloseSafetyEvidenceId).HasMaxLength(200);
        b.Property(p => p.ReviewNotes).HasMaxLength(1000);
        b.Property(p => p.ReminderOffsetsJson).HasMaxLength(4000).HasDefaultValue("[]");
        b.Property(p => p.ReminderPolicyVersion).HasMaxLength(100);
        b.HasOne<User>().WithMany().HasForeignKey(p => p.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => new { p.CreatedByUserId, p.OperationKey }).IsUnique().HasFilter("[OperationKey] IS NOT NULL");
        b.HasIndex(p => new { p.StartsAt, p.ClosedAt, p.CancelledAt });
        b.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
