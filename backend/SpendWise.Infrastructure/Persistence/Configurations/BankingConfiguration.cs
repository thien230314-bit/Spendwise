using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Common;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// Cấu hình khóa ngoại và index giúp ánh xạ dữ liệu provider đúng người dùng, đồng thời chống ghi lặp.
public sealed class BankConnectionConfiguration : IEntityTypeConfiguration<BankConnection>
{
    public void Configure(EntityTypeBuilder<BankConnection> builder)
    {
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasFilter("[Status] = 'Active'");
        builder.HasIndex(x => new { x.Provider, x.AccountEnvironment, x.ExternalAccountId })
            .IsUnique()
            .HasFilter("[Status] = 'Active'");
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.AccountEnvironment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExternalAccountId).HasMaxLength(100);
        builder.Property(x => x.BankBrandName).HasMaxLength(100);
        builder.Property(x => x.AccountType).HasMaxLength(80);
        builder.Property(x => x.AccountHolderName).HasMaxLength(160);
        builder.Property(x => x.MaskedAccountNumber).HasMaxLength(40);
        builder.HasOne(x => x.User)
            .WithMany(x => x.BankConnections)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BankLinkSessionConfiguration : IEntityTypeConfiguration<BankLinkSession>
{
    public void Configure(EntityTypeBuilder<BankLinkSession> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.Provider, x.AccountEnvironment, x.ExternalLinkTokenXid }).IsUnique();
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.AccountEnvironment).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExternalLinkTokenXid).HasMaxLength(100);
        builder.Property(x => x.ExternalBankAccountId).HasMaxLength(100);
        builder.HasOne(x => x.User)
            .WithMany(x => x.BankLinkSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.BankConnection)
            .WithMany()
            .HasForeignKey(x => x.BankConnectionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WalletTopUpConfiguration : IEntityTypeConfiguration<WalletTopUp>
{
    public void Configure(EntityTypeBuilder<WalletTopUp> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Reference).IsUnique();
        // Mỗi tài khoản SePay chỉ có một yêu cầu đang chờ hoặc đang xử lý tại cùng thời điểm.
        builder.HasIndex(x => x.BankConnectionId)
            .IsUnique()
            .HasFilter("[Status] IN ('Pending', 'Processing')");
        builder.HasIndex(x => x.ProviderTransactionId)
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL");
        builder.HasIndex(x => x.LedgerTransactionId).IsUnique()
            .HasFilter("[LedgerTransactionId] IS NOT NULL");
        builder.HasIndex(x => x.FinancialTransactionId).IsUnique()
            .HasFilter("[FinancialTransactionId] IS NOT NULL");
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Reference).HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BankConnection).WithMany().HasForeignKey(x => x.BankConnectionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.LedgerTransaction).WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FinancialTransaction).WithMany().HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class WalletWithdrawalConfiguration : IEntityTypeConfiguration<WalletWithdrawal>
{
    public void Configure(EntityTypeBuilder<WalletWithdrawal> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] <> ''");
        builder.HasIndex(x => x.BankConnectionId)
            .IsUnique()
            .HasFilter("[Status] IN ('Pending', 'Reserved', 'Processing', 'PendingReconciliation')");
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Reference).HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
        builder.Property(x => x.ProviderResponseData).HasMaxLength(4000);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BankConnection).WithMany().HasForeignKey(x => x.BankConnectionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReservationLedgerTransaction).WithMany().HasForeignKey(x => x.ReservationLedgerTransactionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.LedgerTransaction).WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FinancialTransaction).WithMany().HasForeignKey(x => x.FinancialTransactionId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.BankConnectionId);
        builder.HasIndex(x => x.TransactionDate);
        // Unique index là lớp bảo vệ cuối cùng khi hai IPN đến đồng thời.
        builder.HasIndex(x => new { x.Provider, x.ExternalTransactionId }).IsUnique();
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExternalTransactionId).HasMaxLength(150);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Content).HasMaxLength(1000);
        builder.Property(x => x.ReferenceCode).HasMaxLength(200);
        builder.Property(x => x.PaymentCode).HasMaxLength(200);
        builder.Property(x => x.Gateway).HasMaxLength(100);
        builder.HasOne(x => x.User)
            .WithMany(x => x.BankTransactions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BankConnection)
            .WithMany(x => x.Transactions)
            .HasForeignKey(x => x.BankConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class BankIntegrationEventConfiguration : IEntityTypeConfiguration<BankIntegrationEvent>
{
    public void Configure(EntityTypeBuilder<BankIntegrationEvent> builder)
    {
        builder.HasIndex(x => new { x.Provider, x.ExternalEventId })
            .IsUnique()
            .HasFilter("[ExternalEventId] IS NOT NULL");
        builder.HasIndex(x => x.ExternalTransactionId);
        builder.HasIndex(x => x.ProcessingStatus);
        builder.HasIndex(x => x.ReceivedAt);
        builder.Property(x => x.Provider).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExternalEventId).HasMaxLength(150);
        builder.Property(x => x.ExternalTransactionId).HasMaxLength(150);
        builder.Property(x => x.EventType).HasMaxLength(100);
        builder.Property(x => x.ExternalBankAccountId).HasMaxLength(100);
        builder.Property(x => x.PayloadHash).HasMaxLength(64);
        builder.Property(x => x.FailureCode).HasMaxLength(100);
        builder.Property(x => x.DuplicateDeliveryCount).HasDefaultValue(0);
    }
}
