using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Common;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình Category và lịch sử FinancialTransaction của từng người dùng.

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // Index theo chủ sở hữu, loại và tên hỗ trợ kiểm tra danh mục trùng trong đúng phạm vi.
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Key).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Icon).HasMaxLength(50);
        builder.Property(x => x.Color).HasMaxLength(20);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.HasIndex(x => new { x.OwnerUserId, x.Kind, x.Name });
        builder.HasData(SystemCategoryDefaults.All);
    }
}

// Bộ danh mục hệ thống là cấu hình sản phẩm, không phải dữ liệu demo của DevelopmentSeeder.
internal static class SystemCategoryDefaults
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static Category Category(string id, string key, string name, CategoryKind kind, string icon, string color) => new()
    {
        Id = Guid.Parse(id), Key = key, Name = name, Kind = kind, Icon = icon, Color = color,
        IsSystem = true, IsActive = true, CreatedAt = CreatedAt
    };

    public static readonly Category[] All =
    [
        Category("10000000-0000-0000-0000-000000000001", "categories.salary", "Lương & Thưởng", CategoryKind.Income, "Briefcase", "#10B981"),
        Category("10000000-0000-0000-0000-000000000002", "categories.p2p", "Chuyển khoản", CategoryKind.Income, "Send", "#0D9488"),
        Category("10000000-0000-0000-0000-000000000003", "categories.investments", "Đầu tư & Sinh lời", CategoryKind.Income, "TrendingUp", "#3B82F6"),
        Category("10000000-0000-0000-0000-000000000004", "categories.income_other", "Thu nhập khác", CategoryKind.Income, "Plus", "#6B7280"),
        Category("10000000-0000-0000-0000-000000000005", "categories.food", "Ăn uống", CategoryKind.Expense, "Utensils", "#F59E0B"),
        Category("10000000-0000-0000-0000-000000000006", "categories.shopping", "Mua sắm", CategoryKind.Expense, "ShoppingBag", "#EC4899"),
        Category("10000000-0000-0000-0000-000000000007", "categories.transport", "Đi lại", CategoryKind.Expense, "Car", "#0EA5E9"),
        Category("10000000-0000-0000-0000-000000000008", "categories.entertainment", "Giải trí", CategoryKind.Expense, "Tv", "#8B5CF6"),
        Category("10000000-0000-0000-0000-000000000009", "categories.utilities", "Hóa đơn & Nhà ở", CategoryKind.Expense, "Home", "#14B8A6"),
        Category("10000000-0000-0000-0000-00000000000a", "categories.education", "Giáo dục & Học tập", CategoryKind.Expense, "GraduationCap", "#6366F1"),
        Category("10000000-0000-0000-0000-00000000000b", "categories.savings_goals", "Mục tiêu tiết kiệm", CategoryKind.Expense, "Target", "#10B981"),
        Category("10000000-0000-0000-0000-00000000000c", "categories.expense_other", "Chi tiêu khác", CategoryKind.Expense, "Tag", "#6B7280")
    ];
}

public sealed class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        // Index theo chủ sở hữu/thời gian và giữ liên kết LedgerTransaction để bảo toàn lịch sử.
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(40);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CounterpartyName).HasMaxLength(160);
        builder.Property(x => x.CorrectionAllowance).HasDefaultValue(0);
        builder.Property(x => x.CorrectionCount).HasDefaultValue(0);
        builder.HasIndex(x => new { x.OwnerUserId, x.OccurredAt });
        builder.HasOne(x => x.LedgerTransaction)
            .WithMany()
            .HasForeignKey(x => x.LedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionWalletAllocationConfiguration : IEntityTypeConfiguration<TransactionWalletAllocation>
{
    public void Configure(EntityTypeBuilder<TransactionWalletAllocation> builder)
    {
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.HasIndex(x => new { x.FinancialTransactionId, x.WalletId }).IsUnique();
        builder.HasOne(x => x.FinancialTransaction)
            .WithMany(x => x.WalletAllocations)
            .HasForeignKey(x => x.FinancialTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Wallet)
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EvidenceAttachmentConfiguration : IEntityTypeConfiguration<EvidenceAttachment>
{
    public void Configure(EntityTypeBuilder<EvidenceAttachment> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_EvidenceAttachments_OneParent",
            "[FinancialTransactionId] IS NULL OR [TransactionCorrectionId] IS NULL"));
        builder.Property(x => x.OriginalFileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        builder.HasIndex(x => x.ContentHash);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FinancialTransaction)
            .WithMany(x => x.Evidence)
            .HasForeignKey(x => x.FinancialTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TransactionCorrection)
            .WithMany(x => x.Evidence)
            .HasForeignKey(x => x.TransactionCorrectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionCorrectionConfiguration : IEntityTypeConfiguration<TransactionCorrection>
{
    public void Configure(EntityTypeBuilder<TransactionCorrection> builder)
    {
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.OriginalValuesJson).HasMaxLength(4000);
        builder.Property(x => x.CorrectedValuesJson).HasMaxLength(4000);
        builder.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.OriginalDescription).HasMaxLength(500);
        builder.Property(x => x.CorrectedDescription).HasMaxLength(500);
        builder.Property(x => x.OriginalCounterpartyName).HasMaxLength(160);
        builder.Property(x => x.CorrectedCounterpartyName).HasMaxLength(160);
        builder.Property(x => x.OriginalAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.CorrectedAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.HasIndex(x => new { x.TransactionId, x.CorrectionNumber }).IsUnique();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transaction)
            .WithMany(x => x.Corrections)
            .HasForeignKey(x => x.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.OriginalCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CorrectedCategory)
            .WithMany()
            .HasForeignKey(x => x.CorrectedCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(x => x.OriginalWalletId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(x => x.CorrectedWalletId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(x => x.OriginalLedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(x => x.ReversalLedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LedgerTransaction>()
            .WithMany()
            .HasForeignKey(x => x.CorrectedLedgerTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionCorrectionWalletAllocationConfiguration
    : IEntityTypeConfiguration<TransactionCorrectionWalletAllocation>
{
    public void Configure(EntityTypeBuilder<TransactionCorrectionWalletAllocation> builder)
    {
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.HasIndex(x => new { x.TransactionCorrectionId, x.IsOriginal, x.WalletId }).IsUnique();
        builder.HasOne(x => x.TransactionCorrection)
            .WithMany(x => x.WalletAllocations)
            .HasForeignKey(x => x.TransactionCorrectionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Wallet)
            .WithMany()
            .HasForeignKey(x => x.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
