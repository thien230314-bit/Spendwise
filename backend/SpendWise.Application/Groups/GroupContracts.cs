namespace SpendWise.Application.Groups;

// Các DTO này mô tả request và response cho nhóm, quỹ chung, chi phí chung và khoản nợ.

public sealed record CreateGroupRequest(string Name, string? Description, string Type, string Currency = "VND", string? SecurityPin = null);
public sealed record UpdateGroupRequest(string Name, string? Description, bool MembersCanCreateExpenses);
public sealed record GroupMemberDto(Guid UserId, string FullName, string SpendId, string Role, DateTime JoinedAt);
public sealed record GroupDto(Guid Id, string Name, string? Description, string Type, string Status, bool MembersCanCreateExpenses, string CurrentUserRole, IReadOnlyList<GroupMemberDto> Members,
    string Currency = "VND", Guid? OwnerId = null, GroupReceivingBankDto? ReceivingBank = null);
public sealed record InviteGroupRequest(Guid UserId);
public sealed record GroupInviteDto(Guid Id, Guid GroupId, string GroupName, Guid InvitedUserId, string Status, DateTime ExpiresAt, string Token);
public sealed record SharedFundDto(Guid Id, Guid GroupId, decimal Balance, string Currency);

public sealed record GroupReceivingBankRequest(Guid BankConnectionId);
public sealed record GroupReceivingBankDto(Guid Id, Guid OwnerUserId, Guid BankConnectionId, string BankBrand, string MaskedAccount, string AccountHolder);
public sealed record CreateFundContributionPaymentRequest(decimal Amount, string? Purpose, string IdempotencyKey, Guid? WalletId = null, string? SecurityPin = null);
public sealed record GroupContributionDto(Guid Id, Guid GroupId, Guid UserId, Guid WalletId, decimal Amount,
    string Purpose, Guid LedgerTransactionId, DateTime CreatedAt, bool Replayed = false);
public sealed record FundTransactionDto(Guid Id, string Type, decimal Amount, Guid? UserId, string? UserName,
    string Description, DateTime Date, string ReferenceCode, Guid? PaymentId, Guid? WalletId = null,
    Guid? LedgerTransactionId = null);

public sealed record CreateFundRequestRequest(string Purpose, decimal AmountPerMember, DateTime? DueDate, IReadOnlyList<Guid> RecipientUserIds);
public sealed record FundRequestRecipientDto(Guid Id, Guid UserId, string UserName, string Status, Guid? PaymentId, DateTime? PaidAt);
public sealed record FundRequestDto(Guid Id, Guid GroupId, string Purpose, decimal AmountPerMember, DateTime? DueDate, IReadOnlyList<FundRequestRecipientDto> Recipients, DateTime CreatedAt);
public sealed record PayFundRequestRequest(string IdempotencyKey, Guid? WalletId = null, string? SecurityPin = null);

public sealed record CreateFundRoundRequest(string Title, string? Description, decimal TargetAmount);
public sealed record FundRoundMemberDto(Guid Id, Guid UserId, string UserName, decimal RequiredAmount, decimal PaidAmount, string Status);
public sealed record FundRoundDto(Guid Id, Guid GroupId, string Title, string? Description, decimal TargetAmount, decimal CollectedAmount, string Status, DateTime CreatedAt, DateTime? CompletedAt, IReadOnlyList<FundRoundMemberDto> Members);
public sealed record PayFundRoundRequest(decimal Amount, string IdempotencyKey, Guid? WalletId = null, string? SecurityPin = null);

public sealed record CreateGroupExpenseRequest(string Title, decimal Amount, Guid? CategoryId, string? Category, string PaidByType, Guid? PaidByUserId, DateTime? Date, string? Description, string? SecurityPin = null, string? IdempotencyKey = null);
public sealed record GroupExpenseDto(Guid Id, Guid GroupId, string Title, decimal Amount, Guid? CategoryId, string Category, string PaidByType, Guid? PaidByUserId, DateTime Date, string? Description, string Status);
public sealed record GroupActivityDto(Guid Id, Guid? ActorUserId, string Type, string? DataJson, DateTime CreatedAt);

public sealed record GroupDeletionVoteRequest(bool Approved, string? SecurityPin = null);
public sealed record GroupDeletionVoteDto(Guid Id, Guid UserId, string UserName, string Status, DateTime? VotedAt);
public sealed record GroupRefundDto(Guid Id, Guid UserId, string UserName, decimal ContributionBasis,
    decimal RefundAmount, decimal? Percentage, string Status, Guid? PaymentId, string? PaymentReference,
    DateTime? CompletedAt, Guid? DestinationWalletId = null, Guid? LedgerTransactionId = null);
public sealed record GroupDeletionRequestDto(Guid Id, Guid GroupId, string Status, decimal FundBalanceSnapshot, DateTime CreatedAt, DateTime? ReadyAt, DateTime? CompletedAt, IReadOnlyList<GroupDeletionVoteDto> Votes, IReadOnlyList<GroupRefundDto> Refunds);
public sealed record CreateGroupRefundPaymentRequest(string IdempotencyKey, string? SecurityPin = null, Guid? WalletId = null);
public sealed record FinalizeGroupDeletionRequest(string SecurityPin);
