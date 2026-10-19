using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Common;
using SpendWise.Domain.Finance;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình Transfer nội bộ và các quan hệ không cascade delete.

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        // Scope và IdempotencyKey chỉ duy nhất trong phạm vi người gửi; quan hệ User không cascade delete.
        builder.HasIndex(x => new { x.SenderUserId, x.IdempotencyScope, x.IdempotencyKey })
            .IsUnique();
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IdempotencyScope).HasMaxLength(150);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.HasOne(x => x.SenderUser)
            .WithMany()
            .HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientUser)
            .WithMany()
            .HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
