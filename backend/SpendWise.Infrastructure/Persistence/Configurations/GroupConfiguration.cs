using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpendWise.Domain.Common;
using SpendWise.Domain.Groups;

namespace SpendWise.Infrastructure.Persistence.Configurations;

// File này cấu hình membership, quỹ, chi phí, phần chia và khoản nợ của Group.

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        // Giới hạn metadata và lưu Type/Status dạng chuỗi dễ kiểm tra trong database.
        builder.Property(x => x.Name).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30);
    }
}

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        // Unique index ngăn một User có hai membership trong cùng Group.
        builder.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        builder.Property(x => x.Role)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupInviteConfiguration : IEntityTypeConfiguration<GroupInvite>
{
    public void Configure(EntityTypeBuilder<GroupInvite> builder)
    {
        // Token lời mời là duy nhất và Status được lưu dạng chuỗi.
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.GroupId, x.InvitedUserId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
        builder.Property(x => x.Token).HasMaxLength(100);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20);
    }
}

public sealed class GroupReceivingBankConfiguration : IEntityTypeConfiguration<GroupReceivingBank>
{
    public void Configure(EntityTypeBuilder<GroupReceivingBank> builder)
    {
        builder.HasIndex(x => x.GroupId).IsUnique();
        builder.Property(x => x.BankBrandSnapshot).HasMaxLength(160);
        builder.Property(x => x.MaskedAccountSnapshot).HasMaxLength(100);
        builder.Property(x => x.AccountHolderSnapshot).HasMaxLength(160);
        builder.HasOne(x => x.Group).WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Banking.BankConnection>().WithMany().HasForeignKey(x => x.BankConnectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FundTransactionConfiguration : IEntityTypeConfiguration<FundTransaction>
{
    public void Configure(EntityTypeBuilder<FundTransaction> builder)
    {
        builder.HasIndex(x => x.PaymentId).IsUnique().HasFilter("[PaymentId] IS NOT NULL");
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.UserNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ReferenceCode).HasMaxLength(100);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Banking.BankDirectPayment>().WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupContributionConfiguration : IEntityTypeConfiguration<GroupContribution>
{
    public void Configure(EntityTypeBuilder<GroupContribution> builder)
    {
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.Wallet>().WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupFundShareConfiguration : IEntityTypeConfiguration<GroupFundShare>
{
    public void Configure(EntityTypeBuilder<GroupFundShare> builder)
    {
        builder.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
        builder.Property(x => x.RemainingAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupExpenseShareAllocationConfiguration : IEntityTypeConfiguration<GroupExpenseShareAllocation>
{
    public void Configure(EntityTypeBuilder<GroupExpenseShareAllocation> builder)
    {
        builder.HasIndex(x => new { x.GroupExpenseId, x.UserId }).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.HasOne(x => x.GroupExpense).WithMany(x => x.ShareAllocations).HasForeignKey(x => x.GroupExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FundRequestConfiguration : IEntityTypeConfiguration<FundRequest>
{
    public void Configure(EntityTypeBuilder<FundRequest> builder)
    {
        builder.Property(x => x.Purpose).HasMaxLength(500);
        builder.Property(x => x.AmountPerMember).HasPrecision(Money.Precision, Money.Scale);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class FundRequestRecipientConfiguration : IEntityTypeConfiguration<FundRequestRecipient>
{
    public void Configure(EntityTypeBuilder<FundRequestRecipient> builder)
    {
        builder.HasIndex(x => new { x.FundRequestId, x.UserId }).IsUnique();
        builder.Property(x => x.UserNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.FundRequest).WithMany(x => x.Recipients).HasForeignKey(x => x.FundRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupFundRoundConfiguration : IEntityTypeConfiguration<GroupFundRound>
{
    public void Configure(EntityTypeBuilder<GroupFundRound> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(160);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.TargetAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.CollectedAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupFundRoundMemberConfiguration : IEntityTypeConfiguration<GroupFundRoundMember>
{
    public void Configure(EntityTypeBuilder<GroupFundRoundMember> builder)
    {
        builder.HasIndex(x => new { x.GroupFundRoundId, x.UserId }).IsUnique();
        builder.Property(x => x.UserNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.RequiredAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.PaidAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Round).WithMany(x => x.Members).HasForeignKey(x => x.GroupFundRoundId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupExpenseConfiguration : IEntityTypeConfiguration<GroupExpense>
{
    public void Configure(EntityTypeBuilder<GroupExpense> builder)
    {
        builder.Property(x => x.Title).HasMaxLength(160);
        builder.Property(x => x.Amount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.CategorySnapshot).HasMaxLength(160);
        builder.Property(x => x.PaidByType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.HasIndex(x => new { x.GroupId, x.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.PaidByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupActivityConfiguration : IEntityTypeConfiguration<GroupActivity>
{
    public void Configure(EntityTypeBuilder<GroupActivity> builder)
    {
        builder.HasIndex(x => new { x.GroupId, x.CreatedAt });
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.DataJson).HasMaxLength(2000);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupDeletionRequestConfiguration : IEntityTypeConfiguration<GroupDeletionRequest>
{
    public void Configure(EntityTypeBuilder<GroupDeletionRequest> builder)
    {
        builder.HasIndex(x => x.GroupId).IsUnique().HasFilter("[Status] IN ('Pending', 'Ready')");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FundBalanceSnapshot).HasPrecision(Money.Precision, Money.Scale);
        builder.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupDeletionVoteConfiguration : IEntityTypeConfiguration<GroupDeletionVote>
{
    public void Configure(EntityTypeBuilder<GroupDeletionVote> builder)
    {
        builder.HasIndex(x => new { x.GroupDeletionRequestId, x.UserId }).IsUnique();
        builder.Property(x => x.UserNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Request).WithMany(x => x.Votes).HasForeignKey(x => x.GroupDeletionRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GroupRefundConfiguration : IEntityTypeConfiguration<GroupRefund>
{
    public void Configure(EntityTypeBuilder<GroupRefund> builder)
    {
        builder.HasIndex(x => new { x.GroupDeletionRequestId, x.UserId }).IsUnique();
        builder.Property(x => x.UserNameSnapshot).HasMaxLength(160);
        builder.Property(x => x.ContributionBasis).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.RefundAmount).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Percentage).HasPrecision(9, 6);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PaymentReference).HasMaxLength(64);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100);
        builder.HasOne(x => x.Request).WithMany(x => x.Refunds).HasForeignKey(x => x.GroupDeletionRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Identity.User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Banking.BankConnection>().WithMany().HasForeignKey(x => x.RecipientBankConnectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.Wallet>().WithMany().HasForeignKey(x => x.DestinationWalletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SpendWise.Domain.Finance.LedgerTransaction>().WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SharedFundConfiguration : IEntityTypeConfiguration<SharedFund>
{
    public void Configure(EntityTypeBuilder<SharedFund> builder)
    {
        // Mỗi Group chỉ có một SharedFund; quan hệ Group và LedgerAccount không cascade delete.
        builder.HasIndex(x => x.GroupId).IsUnique();
        builder.Property(x => x.Balance).HasPrecision(Money.Precision, Money.Scale);
        builder.Property(x => x.Currency).HasMaxLength(3);
        builder.HasOne(x => x.Group)
            .WithOne(x => x.SharedFund)
            .HasForeignKey<SharedFund>(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LedgerAccount)
            .WithMany()
            .HasForeignKey(x => x.LedgerAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
