using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Common;
using SpendWise.Domain.Identity;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Planning;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Notifications;
using SpendWise.Domain.AI;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Social;
using SpendWise.Domain.MobilePush;

namespace SpendWise.Infrastructure.Persistence;

public sealed class SpendWiseDbContext(DbContextOptions<SpendWiseDbContext> options, IClock clock)
    : DbContext(options), ISpendWiseDbContext
{
    public DbSet<V1UserActivity> V1UserActivities => Set<V1UserActivity>();
    public DbSet<SpendWise.Domain.V1Groups.V1Election> V1Elections => Set<SpendWise.Domain.V1Groups.V1Election>();
    public DbSet<SpendWise.Domain.V1Groups.V1ElectionVoter> V1ElectionVoters => Set<SpendWise.Domain.V1Groups.V1ElectionVoter>();
    public DbSet<SpendWise.Domain.V1Groups.V1ElectionCandidate> V1ElectionCandidates => Set<SpendWise.Domain.V1Groups.V1ElectionCandidate>();
    public DbSet<SpendWise.Domain.V1Groups.V1ElectionBallot> V1ElectionBallots => Set<SpendWise.Domain.V1Groups.V1ElectionBallot>();
    public DbSet<SpendWise.Domain.V1Groups.V1ActivityProposal> V1ActivityProposals => Set<SpendWise.Domain.V1Groups.V1ActivityProposal>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundRequest> V1FundRequests => Set<SpendWise.Domain.V1Groups.V1FundRequest>();
    public DbSet<SpendWise.Domain.V1Groups.V1ActivityVoter> V1ActivityVoters => Set<SpendWise.Domain.V1Groups.V1ActivityVoter>();
    public DbSet<V1MaintenanceControl> V1MaintenanceControls => Set<V1MaintenanceControl>();
    public DbSet<V1MaintenancePlan> V1MaintenancePlans => Set<V1MaintenancePlan>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundTreasury> V1FundTreasuries => Set<SpendWise.Domain.V1Groups.V1FundTreasury>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundObligation> V1FundObligations => Set<SpendWise.Domain.V1Groups.V1FundObligation>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundContribution> V1FundContributions => Set<SpendWise.Domain.V1Groups.V1FundContribution>();
    public DbSet<SpendWise.Domain.V1Groups.V1Fund> V1Funds => Set<SpendWise.Domain.V1Groups.V1Fund>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundMembership> V1FundMemberships => Set<SpendWise.Domain.V1Groups.V1FundMembership>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundInvitation> V1FundInvitations => Set<SpendWise.Domain.V1Groups.V1FundInvitation>();
    public DbSet<SpendWise.Domain.V1Groups.V1OwnershipOffer> V1OwnershipOffers => Set<SpendWise.Domain.V1Groups.V1OwnershipOffer>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundActivity> V1FundActivities => Set<SpendWise.Domain.V1Groups.V1FundActivity>();
    public DbSet<SpendWise.Domain.V1Groups.V1FundCodeCounter> V1FundCodeCounters => Set<SpendWise.Domain.V1Groups.V1FundCodeCounter>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<LoginChallenge> LoginChallenges => Set<LoginChallenge>();
    public DbSet<SecurityPinResetToken> SecurityPinResetTokens => Set<SecurityPinResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<FriendRequest> FriendRequests => Set<FriendRequest>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<FriendPreference> FriendPreferences => Set<FriendPreference>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<TransactionWalletAllocation> TransactionWalletAllocations => Set<TransactionWalletAllocation>();
    public DbSet<EvidenceAttachment> EvidenceAttachments => Set<EvidenceAttachment>();
    public DbSet<TransactionCorrection> TransactionCorrections => Set<TransactionCorrection>();
    public DbSet<TransactionCorrectionWalletAllocation> TransactionCorrectionWalletAllocations => Set<TransactionCorrectionWalletAllocation>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalMovement> GoalMovements => Set<GoalMovement>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<RecurringOccurrence> RecurringOccurrences => Set<RecurringOccurrence>();
    public DbSet<TransactionRule> TransactionRules => Set<TransactionRule>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<GroupInvite> GroupInvites => Set<GroupInvite>();
    public DbSet<SharedFund> SharedFunds => Set<SharedFund>();
    public DbSet<GroupContribution> GroupContributions => Set<GroupContribution>();
    public DbSet<GroupFundShare> GroupFundShares => Set<GroupFundShare>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationDeduplication> NotificationDeduplications => Set<NotificationDeduplication>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();
    public DbSet<MobilePushInstallation> MobilePushInstallations => Set<MobilePushInstallation>();
    public DbSet<MobilePushDelivery> MobilePushDeliveries => Set<MobilePushDelivery>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<AiAttachment> AiAttachments => Set<AiAttachment>();
    public DbSet<AiActionProposal> AiActionProposals => Set<AiActionProposal>();
    public DbSet<BankConnection> BankConnections => Set<BankConnection>();
    public DbSet<BankLinkSession> BankLinkSessions => Set<BankLinkSession>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<BankIntegrationEvent> BankIntegrationEvents => Set<BankIntegrationEvent>();
    public DbSet<WalletTopUp> WalletTopUps => Set<WalletTopUp>();
    public DbSet<BankDirectPayment> BankDirectPayments => Set<BankDirectPayment>();
    public DbSet<GroupReceivingBank> GroupReceivingBanks => Set<GroupReceivingBank>();
    public DbSet<FundTransaction> FundTransactions => Set<FundTransaction>();
    public DbSet<FundRequest> FundRequests => Set<FundRequest>();
    public DbSet<FundRequestRecipient> FundRequestRecipients => Set<FundRequestRecipient>();
    public DbSet<GroupFundRound> GroupFundRounds => Set<GroupFundRound>();
    public DbSet<GroupFundRoundMember> GroupFundRoundMembers => Set<GroupFundRoundMember>();
    public DbSet<GroupExpense> GroupExpenses => Set<GroupExpense>();
    public DbSet<GroupExpenseShareAllocation> GroupExpenseShareAllocations => Set<GroupExpenseShareAllocation>();
    public DbSet<GroupActivity> GroupActivities => Set<GroupActivity>();
    public DbSet<GroupDeletionRequest> GroupDeletionRequests => Set<GroupDeletionRequest>();
    public DbSet<GroupDeletionVote> GroupDeletionVotes => Set<GroupDeletionVote>();
    public DbSet<GroupRefund> GroupRefunds => Set<GroupRefund>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AccountRecoveryRequest> AccountRecoveryRequests => Set<AccountRecoveryRequest>();
    public DbSet<AccountRecoveryReview> AccountRecoveryReviews => Set<AccountRecoveryReview>();
    public DbSet<RiskCase> RiskCases => Set<RiskCase>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<SupportCase> SupportCases => Set<SupportCase>();
    public DbSet<CaseHistory> CaseHistories => Set<CaseHistory>();
    public DbSet<CaseAttachment> CaseAttachments => Set<CaseAttachment>();
    public DbSet<SystemMaintenanceState> SystemMaintenanceStates => Set<SystemMaintenanceState>();
    public DbSet<EmergencyAnnouncement> EmergencyAnnouncements => Set<EmergencyAnnouncement>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Nạp toàn bộ cấu hình entity và điều chỉnh rowversion cho môi trường test SQLite.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SpendWiseDbContext).Assembly);

        // Provider test không tự sinh rowversion giống SQL Server nên cho phép test tự gán token.
        if (UsesTestDatabaseProvider())
        {
            modelBuilder.Entity<Wallet>().Property(x => x.RowVersion).ValueGeneratedNever();
            modelBuilder.Entity<LedgerAccount>().Property(x => x.RowVersion).ValueGeneratedNever();
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Mô phỏng rowversion SQL Server trong SQLite/InMemory để test phát hiện ghi đè đồng thời.
        if (UsesTestDatabaseProvider())
        {
            AssignTestRowVersions(ChangeTracker.Entries<Wallet>());
            AssignTestRowVersions(ChangeTracker.Entries<LedgerAccount>());
        }

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = clock.UtcNow;
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = clock.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private bool UsesTestDatabaseProvider() => Database.ProviderName is
        "Microsoft.EntityFrameworkCore.Sqlite" or
        "Microsoft.EntityFrameworkCore.InMemory";

    private static void AssignTestRowVersions<TEntity>(
        IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity>> entries)
        where TEntity : class
    {
        // Mỗi lần INSERT/UPDATE nhận token mới để context dùng token cũ sẽ bị concurrency conflict.
        foreach (var entry in entries.Where(candidate => candidate.State is
                     EntityState.Added or EntityState.Modified))
        {
            entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        }
    }

    public DbSet<V1Broadcast> V1Broadcasts => Set<V1Broadcast>();

    public void ClearTrackedEntities() => ChangeTracker.Clear();

    public async Task AcquireMaintenanceAdmissionAsync(bool exclusive, CancellationToken ct = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory") return;
        var transaction = Database.CurrentTransaction ?? throw new InvalidOperationException("Maintenance admission requires a transaction.");
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") return;
        if (!Database.IsSqlServer()) throw new InvalidOperationException("Unsupported maintenance admission provider.");
        await using var command = Database.GetDbConnection().CreateCommand(); command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = 'V1:maintenance:financial', @LockMode = @mode, @LockOwner = 'Transaction', @LockTimeout = 0; SELECT @result;";
        var mode = command.CreateParameter(); mode.ParameterName = "@mode"; mode.Value = exclusive ? "Exclusive" : "Shared"; command.Parameters.Add(mode);
        var result = await command.ExecuteScalarAsync(ct);
        if (result is not int code) throw new InvalidOperationException("Maintenance admission returned an invalid result.");
        if (code >= 0) return;
        if (code == -1) throw new SpendWise.Application.Common.Exceptions.AppException("maintenance_admission_busy", "Financial operations are still settling; retry later.", exclusive ? 409 : 503);
        if (code == -2) throw new OperationCanceledException("Maintenance admission canceled.", ct);
        throw new InvalidOperationException("Maintenance admission failed; transaction must unwind.");
    }

    public async Task<bool> TryAcquireEmailVerificationLockAsync(
        Guid userId,
        string? requestIpHash,
        CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            return true; // Existing single-thread test provider; no production concurrency claim.

        var transaction = Database.CurrentTransaction
            ?? throw new InvalidOperationException("OTP admission requires a transaction.");
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite test transactions serialize writers; not proof of SQL Server locking.
            await Users.Where(user => user.Id == userId).ExecuteUpdateAsync(
                setters => setters.SetProperty(user => user.UpdatedAt, user => user.UpdatedAt),
                cancellationToken);
            return true;
        }
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new InvalidOperationException("Unsupported OTP admission database provider.");

        // Transaction-owned SQL locks work across API instances sharing this database.
        // Account admission never waits. Shared-IP admission waits at most five seconds
        // so distinct accounts can recheck the existing IP quota without a false busy denial.
        // https://learn.microsoft.com/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql
        var resources = requestIpHash is null
            ? new[] { $"otp:user:{userId:N}" }
            : new[] { $"otp:user:{userId:N}", $"otp:ip:{requestIpHash}" };
        foreach (var resource in resources)
        {
            await using var command = Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource = @resource,
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout;
                SELECT @result;
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.Value = resource;
            command.Parameters.Add(parameter);
            var timeout = command.CreateParameter();
            timeout.ParameterName = "@timeout";
            timeout.Value = resource.StartsWith("otp:ip:", StringComparison.Ordinal) ? 5000 : 0;
            command.Parameters.Add(timeout);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (!InterpretEmailVerificationLockResult(result, cancellationToken))
                return false;
        }
        return true;
    }

    private static bool InterpretEmailVerificationLockResult(object? result, CancellationToken cancellationToken)
    {
        if (result is not int code)
            throw new InvalidOperationException("OTP admission lock returned an invalid result.");

        // An application-lock deadlock does not roll back its transaction in SQL Server.
        // Only timeout is an ordinary denial; other failures must unwind the transaction.
        return code switch
        {
            >= 0 => true,
            -1 => false,
            -2 => throw new OperationCanceledException("OTP admission lock was canceled.", cancellationToken),
            -3 => throw new InvalidOperationException("OTP admission lock could not be acquired."),
            -999 => throw new InvalidOperationException("OTP admission lock could not be initialized."),
            _ => throw new InvalidOperationException("OTP admission lock returned an unexpected result.")
        };
    }

    public async Task<bool> AcquireGroupAdmissionLockAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational()) return await Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (Database.CurrentTransaction is null) throw new InvalidOperationException("Group admission requires a transaction.");
        if (Database.IsSqlServer())
        {
            var locked = await Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {userId}")
                .AsNoTracking().Select(u => (Guid?)u.Id).SingleOrDefaultAsync(cancellationToken);
            return locked is not null;
        }
        return await Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, u => u.Status), cancellationToken) == 1;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational() || Database.CurrentTransaction is not null)
            return await operation(cancellationToken);

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
