using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Notifications;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình nội dung Notification và index phục vụ danh sách chưa đọc.

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // Index theo UserId, ReadAt và CreatedAt phục vụ danh sách thông báo mới nhất/chưa đọc.
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(40);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.DataJson).HasMaxLength(4000);
        builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
    }
}

// Cấu hình outbox để worker tìm nhanh các sự kiện chưa gửi hoặc cần thử lại.
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(x => x.EventName).HasMaxLength(100);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt, x.CreatedAt });
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.AccountKey }).IsUnique();
        builder.Property(x => x.AccountKey).HasMaxLength(100);
        builder.Property(x => x.AccountName).HasMaxLength(160);
        builder.Property(x => x.LargeTransactionThreshold).HasPrecision(18, 2);
        builder.Property(x => x.MinBalanceThreshold).HasPrecision(18, 2);
    }
}

public sealed class NotificationDeduplicationConfiguration : IEntityTypeConfiguration<NotificationDeduplication>
{
    public void Configure(EntityTypeBuilder<NotificationDeduplication> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
        builder.Property(x => x.Key).HasMaxLength(200);
    }
}
