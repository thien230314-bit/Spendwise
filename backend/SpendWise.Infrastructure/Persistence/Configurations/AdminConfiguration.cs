using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Admin;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình AuditLog, RiskCase, tranh chấp, hỗ trợ và thiết lập quản trị.

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        // Giới hạn metadata audit và index theo Action/CreatedAt để tra cứu lịch sử.
        b.Property(x => x.Action).HasMaxLength(80);
        b.Property(x => x.TargetType).HasMaxLength(80);
        b.Property(x => x.TargetId).HasMaxLength(100);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.MetadataJson).HasMaxLength(4000);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.HasIndex(x => new { x.Action, x.CreatedAt });
    }
}

public sealed class AccountRecoveryRequestConfiguration : IEntityTypeConfiguration<AccountRecoveryRequest>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryRequest> builder)
    {
        builder.Property(x => x.FullName).HasMaxLength(160);
        builder.Property(x => x.Phone).HasMaxLength(10);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.EmailHint).HasMaxLength(256);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.AdminNote).HasMaxLength(2000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.Phone);
    }
}

public sealed class AccountRecoveryReviewConfiguration : IEntityTypeConfiguration<AccountRecoveryReview>
{
    public void Configure(EntityTypeBuilder<AccountRecoveryReview> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(40);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasIndex(x => new { x.AccountRecoveryRequestId, x.CreatedAt });
        builder.HasOne(x => x.Request)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.AccountRecoveryRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RiskCaseConfiguration : IEntityTypeConfiguration<RiskCase>
{
    public void Configure(EntityTypeBuilder<RiskCase> b)
    {
        // Lưu mức rủi ro, quyết định và trạng thái review dạng chuỗi.
        b.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Signals).HasMaxLength(1000);
    }
}

public sealed class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> b)
    {
        // Giới hạn lý do, ghi chú nội bộ và kết quả xử lý của tranh chấp.
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.TransactionReferenceSnapshot).HasMaxLength(100);
        b.Property(x => x.TransactionTitleSnapshot).HasMaxLength(500);
        b.Property(x => x.TransactionAmountSnapshot).HasPrecision(18, 2);
        b.Property(x => x.CounterpartySnapshot).HasMaxLength(160);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.InternalNotes).HasMaxLength(4000);
        b.Property(x => x.Resolution).HasMaxLength(2000);
    }
}

public sealed class SupportCaseConfiguration : IEntityTypeConfiguration<SupportCase>
{
    public void Configure(EntityTypeBuilder<SupportCase> b)
    {
        b.HasIndex(x => new { x.AssignedAdminId, x.Status, x.DueAt, x.CreatedAt });
        b.Property(x => x.EscalationReason).HasMaxLength(1000);
        b.Property(x => x.EscalationGuidance).HasMaxLength(2000);
        b.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.EscalatedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.EscalationReviewerId).OnDelete(DeleteBehavior.Restrict);
        // Giới hạn loại, mức ưu tiên, trạng thái và ghi chú nội bộ của yêu cầu hỗ trợ.
        b.Property(x => x.Category).HasMaxLength(80);
        b.HasIndex(x => x.TicketNumber).IsUnique();
        b.Property(x => x.TicketNumber).HasMaxLength(40);
        b.Property(x => x.Subject).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.Priority).HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.InternalNotes).HasMaxLength(4000);
        b.Property(x => x.CorrectionGrantReason).HasMaxLength(1000);
    }
}

public sealed class CaseHistoryConfiguration : IEntityTypeConfiguration<CaseHistory>
{
    public void Configure(EntityTypeBuilder<CaseHistory> builder)
    {
        builder.HasIndex(x => new { x.CaseKind, x.CaseId, x.CreatedAt });
        builder.Property(x => x.CaseKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ActorDisplayNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.ActorRole).HasMaxLength(30);
        builder.Property(x => x.Action).HasMaxLength(40);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.OldStatus).HasMaxLength(30);
        builder.Property(x => x.NewStatus).HasMaxLength(30);
    }
}

public sealed class CaseAttachmentConfiguration : IEntityTypeConfiguration<CaseAttachment>
{
    public void Configure(EntityTypeBuilder<CaseAttachment> builder)
    {
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.HasOne(x => x.History).WithMany(x => x.Attachments)
            .HasForeignKey(x => x.CaseHistoryId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SystemMaintenanceStateConfiguration : IEntityTypeConfiguration<SystemMaintenanceState>
{
    public void Configure(EntityTypeBuilder<SystemMaintenanceState> builder) =>
        builder.HasIndex(x => x.IsEnabled);
}

public sealed class EmergencyAnnouncementConfiguration : IEntityTypeConfiguration<EmergencyAnnouncement>
{
    public void Configure(EntityTypeBuilder<EmergencyAnnouncement> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => x.Status).IsUnique().HasFilter("[Status] = 'Active'");
    }
}

public sealed class SystemConfigurationConfiguration : IEntityTypeConfiguration<SystemConfiguration>
{
    public void Configure(EntityTypeBuilder<SystemConfiguration> b)
    {
        // Key cấu hình là duy nhất để mỗi thiết lập vận hành chỉ có một giá trị.
        b.HasIndex(x => x.Key).IsUnique();
        b.Property(x => x.Key).HasMaxLength(80);
        b.Property(x => x.Value).HasMaxLength(500);
        b.Property(x => x.ValueType).HasMaxLength(30);
        b.Property(x => x.Description).HasMaxLength(500);
    }
}

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> b)
    {
        // Tên cờ tính năng là duy nhất để chính sách đọc đúng trạng thái bật/tắt.
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Name).HasMaxLength(80);
        b.Property(x => x.Description).HasMaxLength(500);
    }
}
