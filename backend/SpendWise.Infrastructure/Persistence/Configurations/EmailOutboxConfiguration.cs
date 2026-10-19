using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Notifications;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// Cấu hình outbox email, đặc biệt là khóa chống gửi trùng.
public sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<EmailOutboxMessage> builder)
    {
        builder.Property(x => x.RecipientEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HtmlBody).IsRequired();
        builder.Property(x => x.TextBody).IsRequired();
        builder.Property(x => x.DeduplicationKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LastError).HasMaxLength(1000);
        builder.HasIndex(x => x.DeduplicationKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
    }
}
