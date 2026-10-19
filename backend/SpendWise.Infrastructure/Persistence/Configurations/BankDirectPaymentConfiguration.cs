using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Common;
using SpendWise.Domain.Groups;

namespace SpendWise.Infrastructure.Persistence.Configurations;

public sealed class BankDirectPaymentConfiguration : IEntityTypeConfiguration<BankDirectPayment>
{
    public void Configure(EntityTypeBuilder<BankDirectPayment> builder)
    {
        builder.HasIndex(x => x.PaymentReference).IsUnique();
        builder.HasIndex(x => new { x.SenderUserId, x.ContextType, x.ClientRequestKey }).IsUnique();
        builder.HasIndex(x => x.MatchedBankTransactionId).IsUnique()
            .HasFilter("[MatchedBankTransactionId] IS NOT NULL");
        builder.HasIndex(x => new { x.RecipientBankConnectionId, x.Status, x.Amount });
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.PaymentReference).HasMaxLength(64);
        builder.Property(x => x.ClientRequestKey).HasMaxLength(100);
        builder.Property(x => x.ContextType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RecipientDisplayNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.RecipientPhoneSnapshot).HasMaxLength(30);
        builder.Property(x => x.BankBrandSnapshot).HasMaxLength(160);
        builder.Property(x => x.AccountHolderSnapshot).HasMaxLength(160);
        builder.Property(x => x.MaskedAccountSnapshot).HasMaxLength(100);
        builder.HasOne(x => x.SenderUser).WithMany().HasForeignKey(x => x.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientUser).WithMany().HasForeignKey(x => x.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientBankConnection).WithMany()
            .HasForeignKey(x => x.RecipientBankConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MatchedBankTransaction).WithMany()
            .HasForeignKey(x => x.MatchedBankTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FundRequest>().WithMany().HasForeignKey(x => x.FundRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FundRequestRecipient>().WithMany().HasForeignKey(x => x.FundRequestRecipientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupFundRound>().WithMany().HasForeignKey(x => x.FundRoundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupFundRoundMember>().WithMany().HasForeignKey(x => x.FundRoundMemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupDeletionRequest>().WithMany().HasForeignKey(x => x.DeletionRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupRefund>().WithMany().HasForeignKey(x => x.GroupRefundId).OnDelete(DeleteBehavior.Restrict);
    }
}
