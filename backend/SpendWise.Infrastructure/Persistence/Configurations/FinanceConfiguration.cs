using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Common;
using SpendWise.Domain.Finance;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình Wallet và cấu trúc Ledger dùng để ghi sổ Nợ/Có.

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> b)
    {
        // Lưu số dư theo độ chính xác tiền tệ và tạo index tìm ví mặc định của chủ sở hữu.
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.AvailableBalance).HasPrecision(Money.Precision, Money.Scale);
        b.Property(x => x.RowVersion).IsRowVersion();
        // SQL Server chỉ cho phép mỗi người dùng có nhiều nhất một ví default.
        b.HasIndex(x => x.OwnerUserId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
    }
}
public sealed class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> b)
    {
        // Mã tài khoản Ledger là duy nhất; quan hệ một-một với Wallet không được cascade delete.
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(80);
        b.Property(x => x.Name).HasMaxLength(160);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Balance).HasPrecision(Money.Precision, Money.Scale);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Wallet).WithOne(x => x.LedgerAccount).HasForeignKey<LedgerAccount>(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class LedgerTransactionConfiguration : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> b)
    {
        b.HasIndex(x => new { x.CreatedAt, x.Status });
        // Reference và bộ BusinessType/scope/key là duy nhất để tìm posting hoặc replay idempotency.
        b.HasIndex(x => x.Reference).IsUnique();
        b.HasIndex(x => new { x.BusinessType, x.IdempotencyScope, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");
        b.Property(x => x.Reference).HasMaxLength(40);
        b.Property(x => x.BusinessType).HasMaxLength(50);
        b.Property(x => x.IdempotencyScope).HasMaxLength(150);
        b.Property(x => x.IdempotencyKey).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
    }
}
public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> b)
    {
        // Lưu Amount theo độ chính xác tiền tệ và giữ LedgerEntry khi posting đã tồn tại.
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Memo).HasMaxLength(500);
        b.HasOne(x => x.LedgerTransaction).WithMany(x => x.Entries).HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
