using SpendWise.Domain.Common;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Domain.Groups;

// Các entity này mô tả thành viên, quỹ chung, chi phí chung và nghĩa vụ nợ trong Group.

public enum GroupType { Family, Roommates, Trip, Couple, Friends, Other }
public enum GroupStatus { Active, Suspended, Closed, DeletionPending }
public enum GroupRole { Owner, Member }
public enum InviteStatus { Pending, Accepted, Rejected, Cancelled, Expired }
public enum SplitMethod { Equal, Percentage, ExactAmount, Shares }
public enum ExpensePayerType { User, SharedFund }
public enum DebtStatus { Pending, PartiallyPaid, Settled, Cancelled }
public enum ContributionRequestStatus { Pending, Paid, Overdue, Cancelled }

public sealed class Group : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupType Type { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
    // Closed giữ lại nhóm và lịch sử thay vì xóa cứng.
    public GroupStatus Status { get; set; } = GroupStatus.Active;
    public bool MembersCanCreateExpenses { get; set; } = true;
    public List<GroupMember> Members { get; set; } = [];
    public SharedFund SharedFund { get; set; } = null!;
}
public sealed class GroupMember : BaseEntity
{
    // Entity này chỉ là quan hệ thành viên; xóa nó không xóa User.
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Mỗi nhóm chỉ được có một Owner; chuyển quyền phải hạ Owner cũ và nâng Member mới cùng lúc.
    public GroupRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}
public sealed class GroupInvite : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid InvitedUserId { get; set; }
    public Guid InvitedByUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public DateTime ExpiresAt { get; set; }
}
public sealed class SharedFund : BaseEntity
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid LedgerAccountId { get; set; }
    public LedgerAccount LedgerAccount { get; set; } = null!;
    // Balance hiển thị phải luôn đồng bộ với LedgerAccount riêng của quỹ nhóm.
    public decimal Balance { get; set; }
    public string Currency { get; set; } = Money.DefaultCurrency;
}
public sealed class GroupContribution : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    // Đóng góp quỹ là lịch sử chuyển tiền, không phải Expense.
    public decimal Amount { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid LedgerTransactionId { get; set; }
    public Guid? ContributionRequestRecipientId { get; set; }
}
public sealed class GroupFundShare : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public decimal RemainingAmount { get; set; }
}
public sealed class GroupContributionRequest : BaseEntity
{
    public Guid GroupId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountPerMember { get; set; }
    public DateTime? DueDate { get; set; }
    public List<GroupContributionRecipient> Recipients { get; set; } = [];
}
public sealed class GroupContributionRecipient : BaseEntity
{
    public Guid GroupContributionRequestId { get; set; }
    public GroupContributionRequest Request { get; set; } = null!;
    public Guid UserId { get; set; }
    public ContributionRequestStatus Status { get; set; } = ContributionRequestStatus.Pending;
    public Guid? ContributionId { get; set; }
}
public sealed class SharedExpense : BaseEntity
{
    public Guid GroupId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExpensePayerType PaidByType { get; set; }
    public Guid? PaidByUserId { get; set; }
    public Guid? SharedFundId { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public DateTime Date { get; set; }
    public SplitMethod SplitMethod { get; set; }
    // Splits là căn cứ tạo nghĩa vụ nợ nên SharedExpense không bị CRUD-delete tùy ý.
    public List<ExpenseSplit> Splits { get; set; } = [];
}
public sealed class ExpenseSplit : BaseEntity
{
    public Guid SharedExpenseId { get; set; }
    public SharedExpense SharedExpense { get; set; } = null!;
    public Guid UserId { get; set; }
    public decimal InputValue { get; set; }
    public decimal ShareAmount { get; set; }
}
public sealed class GroupDebt : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid SharedExpenseId { get; set; }
    public Guid ExpenseSplitId { get; set; }
    public Guid DebtorUserId { get; set; }
    public Guid CreditorUserId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    // Chỉ Pending/PartiallyPaid được nhận settlement mới; Cancelled và Settled là trạng thái cuối.
    public DebtStatus Status { get; set; } = DebtStatus.Pending;
    public List<DebtSettlement> Settlements { get; set; } = [];
    // RemainingAmount luôn được tính từ nợ gốc trừ tổng đã thanh toán.
    public decimal RemainingAmount => OriginalAmount - PaidAmount;
}
public sealed class DebtSettlement : BaseEntity
{
    public Guid GroupDebtId { get; set; }
    public GroupDebt GroupDebt { get; set; } = null!;
    public Guid DebtorWalletId { get; set; }
    public Guid CreditorWalletId { get; set; }
    public decimal Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    // Settlement đã posting là lịch sử tài chính bất biến, không Update/Delete bằng CRUD.
    public Guid LedgerTransactionId { get; set; }
}

public enum FundTransactionType { Contribution, Expense }
public enum FundRequestRecipientStatus { Pending, Paid, Overdue, Cancelled }
public enum GroupFundRoundStatus { Active, Completed }
public enum GroupFundRoundMemberStatus { Unpaid, Partial, Paid }
public enum GroupExpensePaidByType { User, Fund }
public enum GroupExpenseStatus { Recorded, Cancelled }
public enum GroupActivityType
{
    Expense,
    FundContribution,
    MemberInvited,
    MemberJoined,
    MemberRemoved,
    Request,
    FundExpense,
    FundRoundCreated,
    FundRoundPaid,
    FundRoundCompleted
}
public enum GroupDeletionRequestStatus { Pending, Rejected, Cancelled, Ready, Completed }
public enum GroupDeletionVoteStatus { Pending, Approved, Rejected }
public enum GroupRefundStatus { Pending, PaymentPending, Completed }

public sealed class GroupReceivingBank : BaseEntity
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;
    public Guid OwnerUserId { get; set; }
    public Guid BankConnectionId { get; set; }
    public string BankBrandSnapshot { get; set; } = string.Empty;
    public string MaskedAccountSnapshot { get; set; } = string.Empty;
    public string AccountHolderSnapshot { get; set; } = string.Empty;
}

public sealed class FundTransaction : BaseEntity
{
    public Guid GroupId { get; set; }
    public FundTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public Guid? UserId { get; set; }
    public string? UserNameSnapshot { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ReferenceCode { get; set; } = string.Empty;
    public Guid? PaymentId { get; set; }
    public Guid? WalletId { get; set; }
    public Guid? LedgerTransactionId { get; set; }
}

public sealed class FundRequest : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountPerMember { get; set; }
    public DateTime? DueDate { get; set; }
    public List<FundRequestRecipient> Recipients { get; set; } = [];
}

public sealed class FundRequestRecipient : BaseEntity
{
    public Guid FundRequestId { get; set; }
    public FundRequest FundRequest { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserNameSnapshot { get; set; } = string.Empty;
    public FundRequestRecipientStatus Status { get; set; } = FundRequestRecipientStatus.Pending;
    public Guid? PaymentId { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class GroupFundRound : BaseEntity
{
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public GroupFundRoundStatus Status { get; set; } = GroupFundRoundStatus.Active;
    public Guid CreatedByUserId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<GroupFundRoundMember> Members { get; set; } = [];
}

public sealed class GroupFundRoundMember : BaseEntity
{
    public Guid GroupFundRoundId { get; set; }
    public GroupFundRound Round { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserNameSnapshot { get; set; } = string.Empty;
    public decimal RequiredAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public GroupFundRoundMemberStatus Status { get; set; } = GroupFundRoundMemberStatus.Unpaid;
}

public sealed class GroupExpense : BaseEntity
{
    public Guid GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid? CategoryId { get; set; }
    public string CategorySnapshot { get; set; } = string.Empty;
    public GroupExpensePaidByType PaidByType { get; set; }
    public Guid? PaidByUserId { get; set; }
    public DateTime Date { get; set; }
    public string? Description { get; set; }
    public GroupExpenseStatus Status { get; set; } = GroupExpenseStatus.Recorded;
    public string? IdempotencyKey { get; set; }
    public List<GroupExpenseShareAllocation> ShareAllocations { get; set; } = [];
}

public sealed class GroupExpenseShareAllocation : BaseEntity
{
    public Guid GroupExpenseId { get; set; }
    public GroupExpense GroupExpense { get; set; } = null!;
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class GroupActivity : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid? ActorUserId { get; set; }
    public GroupActivityType Type { get; set; }
    public string? DataJson { get; set; }
}

public sealed class GroupDeletionRequest : BaseEntity
{
    public Guid GroupId { get; set; }
    public Guid OwnerUserId { get; set; }
    public GroupDeletionRequestStatus Status { get; set; } = GroupDeletionRequestStatus.Pending;
    public decimal FundBalanceSnapshot { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<GroupDeletionVote> Votes { get; set; } = [];
    public List<GroupRefund> Refunds { get; set; } = [];
}

public sealed class GroupDeletionVote : BaseEntity
{
    public Guid GroupDeletionRequestId { get; set; }
    public GroupDeletionRequest Request { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserNameSnapshot { get; set; } = string.Empty;
    public GroupDeletionVoteStatus Status { get; set; } = GroupDeletionVoteStatus.Pending;
    public DateTime? VotedAt { get; set; }
}

public sealed class GroupRefund : BaseEntity
{
    public Guid GroupDeletionRequestId { get; set; }
    public GroupDeletionRequest Request { get; set; } = null!;
    public Guid UserId { get; set; }
    public string UserNameSnapshot { get; set; } = string.Empty;
    public decimal ContributionBasis { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal? Percentage { get; set; }
    public GroupRefundStatus Status { get; set; } = GroupRefundStatus.Pending;
    public Guid? PaymentId { get; set; }
    public string? PaymentReference { get; set; }
    public Guid? RecipientBankConnectionId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? DestinationWalletId { get; set; }
    public Guid? LedgerTransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
}
