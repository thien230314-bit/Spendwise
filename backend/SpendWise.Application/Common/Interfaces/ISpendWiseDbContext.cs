using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Admin;
using SpendWise.Domain.AI;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.MobilePush;
using SpendWise.Domain.Planning;
using SpendWise.Domain.Social;

namespace SpendWise.Application.Common.Interfaces;

// Interface này cho Handler truy cập dữ liệu và transaction mà không phụ thuộc Infrastructure.

public interface ISpendWiseDbContext
{
    DbSet<SpendWise.Domain.V1Groups.V1Election> V1Elections { get; }
    DbSet<SpendWise.Domain.V1Groups.V1ElectionVoter> V1ElectionVoters { get; }
    DbSet<SpendWise.Domain.V1Groups.V1ElectionCandidate> V1ElectionCandidates { get; }
    DbSet<SpendWise.Domain.V1Groups.V1ElectionBallot> V1ElectionBallots { get; }
    DbSet<SpendWise.Domain.V1Groups.V1ActivityProposal> V1ActivityProposals { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundRequest> V1FundRequests { get; }
    DbSet<SpendWise.Domain.V1Groups.V1ActivityVoter> V1ActivityVoters { get; }
    DbSet<V1UserActivity> V1UserActivities { get; }
    DbSet<V1MaintenanceControl> V1MaintenanceControls { get; }
    DbSet<V1MaintenancePlan> V1MaintenancePlans { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundTreasury> V1FundTreasuries { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundObligation> V1FundObligations { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundContribution> V1FundContributions { get; }
    DbSet<SpendWise.Domain.V1Groups.V1Fund> V1Funds { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundMembership> V1FundMemberships { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundInvitation> V1FundInvitations { get; }
    DbSet<SpendWise.Domain.V1Groups.V1OwnershipOffer> V1OwnershipOffers { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundActivity> V1FundActivities { get; }
    DbSet<SpendWise.Domain.V1Groups.V1FundCodeCounter> V1FundCodeCounters { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<LoginChallenge> LoginChallenges { get; }
    DbSet<SecurityPinResetToken> SecurityPinResetTokens { get; }
    DbSet<EmailVerificationToken> EmailVerificationTokens { get; }
    DbSet<FriendRequest> FriendRequests { get; }
    DbSet<Friendship> Friendships { get; }
    DbSet<FriendPreference> FriendPreferences { get; }
    DbSet<Wallet> Wallets { get; }
    DbSet<LedgerAccount> LedgerAccounts { get; }
    DbSet<LedgerTransaction> LedgerTransactions { get; }
    DbSet<LedgerEntry> LedgerEntries { get; }
    DbSet<Category> Categories { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<TransactionWalletAllocation> TransactionWalletAllocations { get; }
    DbSet<EvidenceAttachment> EvidenceAttachments { get; }
    DbSet<TransactionCorrection> TransactionCorrections { get; }
    DbSet<TransactionCorrectionWalletAllocation> TransactionCorrectionWalletAllocations { get; }
    DbSet<Transfer> Transfers { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<Goal> Goals { get; }
    DbSet<GoalMovement> GoalMovements { get; }
    DbSet<RecurringTransaction> RecurringTransactions { get; }
    DbSet<RecurringOccurrence> RecurringOccurrences { get; }
    DbSet<TransactionRule> TransactionRules { get; }
    DbSet<Group> Groups { get; }
    DbSet<GroupMember> GroupMembers { get; }
    DbSet<GroupInvite> GroupInvites { get; }
    DbSet<SharedFund> SharedFunds { get; }
    DbSet<GroupContribution> GroupContributions { get; }
    DbSet<GroupFundShare> GroupFundShares { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<NotificationDeduplication> NotificationDeduplications { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<EmailOutboxMessage> EmailOutboxMessages { get; }
    DbSet<MobilePushInstallation> MobilePushInstallations { get; }
    DbSet<MobilePushDelivery> MobilePushDeliveries { get; }
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<AiAttachment> AiAttachments { get; }
    DbSet<AiActionProposal> AiActionProposals { get; }
    DbSet<BankConnection> BankConnections { get; }
    DbSet<BankLinkSession> BankLinkSessions { get; }
    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<BankIntegrationEvent> BankIntegrationEvents { get; }
    DbSet<WalletTopUp> WalletTopUps { get; }
    DbSet<BankDirectPayment> BankDirectPayments { get; }
    DbSet<GroupReceivingBank> GroupReceivingBanks { get; }
    DbSet<FundTransaction> FundTransactions { get; }
    DbSet<FundRequest> FundRequests { get; }
    DbSet<FundRequestRecipient> FundRequestRecipients { get; }
    DbSet<GroupFundRound> GroupFundRounds { get; }
    DbSet<GroupFundRoundMember> GroupFundRoundMembers { get; }
    DbSet<GroupExpense> GroupExpenses { get; }
    DbSet<GroupExpenseShareAllocation> GroupExpenseShareAllocations { get; }
    DbSet<GroupActivity> GroupActivities { get; }
    DbSet<GroupDeletionRequest> GroupDeletionRequests { get; }
    DbSet<GroupDeletionVote> GroupDeletionVotes { get; }
    DbSet<GroupRefund> GroupRefunds { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<V1Broadcast> V1Broadcasts { get; }
    DbSet<AccountRecoveryRequest> AccountRecoveryRequests { get; }
    DbSet<AccountRecoveryReview> AccountRecoveryReviews { get; }
    DbSet<RiskCase> RiskCases { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<SupportCase> SupportCases { get; }
    DbSet<CaseHistory> CaseHistories { get; }
    DbSet<CaseAttachment> CaseAttachments { get; }
    DbSet<SystemMaintenanceState> SystemMaintenanceStates { get; }
    DbSet<EmergencyAnnouncement> EmergencyAnnouncements { get; }
    DbSet<SystemConfiguration> SystemConfigurations { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // Xóa state EF đã thay đổi sau một transaction rollback trước khi ghi audit độc lập.
    void ClearTrackedEntities();

    Task<bool> AcquireGroupAdmissionLockAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AcquireMaintenanceAdmissionAsync(bool exclusive, CancellationToken cancellationToken = default);

    // Chỉ dùng trong transaction OTP; false nghĩa là chưa giành được admission.
    Task<bool> TryAcquireEmailVerificationLockAsync(
        Guid userId,
        string? requestIpHash,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
