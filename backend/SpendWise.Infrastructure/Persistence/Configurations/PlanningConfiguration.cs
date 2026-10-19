using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Common;
using SpendWise.Domain.Planning;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình Budget, Goal, lịch định kỳ và TransactionRule.

public sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        // Mỗi người chỉ có một Budget cho cùng danh mục, năm và tháng.
        builder.HasIndex(x => new { x.OwnerUserId, x.CategoryId, x.Year, x.Month })
            .IsUnique();
        builder.Property(x => x.LimitAmount)
            .HasPrecision(Money.Precision, Money.Scale);
    }
}

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        // Lưu TargetAmount và CurrentAllocatedAmount chính xác, đồng thời giữ tài khoản Ledger liên kết.
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.TargetAmount)
            .HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.CurrentAllocatedAmount)
            .HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.CategorySnapshot).HasMaxLength(160);
        builder.Property(x => x.IconName).HasMaxLength(80);
        builder.Property(x => x.ColorHex).HasMaxLength(20);
        builder.HasOne(x => x.LedgerAccount)
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GoalMovementConfiguration : IEntityTypeConfiguration<GoalMovement>
{
    public void Configure(EntityTypeBuilder<GoalMovement> builder)
    {
        // Bộ GoalId, Type và IdempotencyKey là duy nhất để không đóng góp hoặc rút tiền hai lần.
        builder.HasIndex(x => new { x.GoalId, x.Type, x.IdempotencyKey }).IsUnique();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.WalletNameSnapshot).HasMaxLength(120);
        builder.HasOne(x => x.Goal)
            .WithMany(x => x.Movements)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
{
    public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
    {
        // Index theo Status và NextRunAt giúp bộ xử lý tìm nhanh các lịch đã đến hạn.
        builder.Property(x => x.Kind)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Title).HasMaxLength(160).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Frequency)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.HasIndex(x => new { x.Status, x.NextRunAt });
    }
}

public sealed class RecurringOccurrenceConfiguration : IEntityTypeConfiguration<RecurringOccurrence>
{
    public void Configure(EntityTypeBuilder<RecurringOccurrence> builder)
    {
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.RecurringTransactionId, x.ScheduledFor }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ScheduledFor });
        builder.HasOne(x => x.RecurringTransaction)
            .WithMany(x => x.Occurrences)
            .HasForeignKey(x => x.RecurringTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedTransaction)
            .WithMany()
            .HasForeignKey(x => x.RecordedTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransactionRuleConfiguration : IEntityTypeConfiguration<TransactionRule>
{
    public void Configure(EntityTypeBuilder<TransactionRule> builder)
    {
        // Index theo chủ sở hữu và Priority hỗ trợ áp dụng rule theo đúng thứ tự.
        builder.Property(x => x.DescriptionPattern).HasMaxLength(160);
        builder.HasIndex(x => new { x.OwnerUserId, x.Priority });
    }
}
