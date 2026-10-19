using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.MobilePush;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class MobilePushInstallationConfiguration
    : IEntityTypeConfiguration<MobilePushInstallation>
{
    public void Configure(EntityTypeBuilder<MobilePushInstallation> builder)
    {
        builder.Property(item => item.Platform).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.PushToken).HasMaxLength(512);
        builder.Property(item => item.DeviceName).HasMaxLength(120);
        builder.HasIndex(item => item.PushToken)
            .IsUnique()
            .HasFilter("[PushToken] IS NOT NULL");
        builder.HasIndex(item => new { item.UserId, item.IsActive });
    }
}

public sealed class MobilePushDeliveryConfiguration
    : IEntityTypeConfiguration<MobilePushDelivery>
{
    public void Configure(EntityTypeBuilder<MobilePushDelivery> builder)
    {
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.ExpoReceiptId).HasMaxLength(100);
        builder.Property(item => item.LastError).HasMaxLength(200);
        builder.HasIndex(item => new { item.NotificationId, item.InstallationId }).IsUnique();
        builder.HasIndex(item => new { item.Status, item.NextAttemptAt, item.CreatedAt });
        builder.HasOne(item => item.Notification)
            .WithMany()
            .HasForeignKey(item => item.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Installation)
            .WithMany()
            .HasForeignKey(item => item.InstallationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
