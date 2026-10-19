using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Admin;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.AdminAi;

public sealed record AdminAiUserSummary(
    int Total, int Active, int Restricted, int Locked, int Suspended, int NewInWindow);
public sealed record AdminAiLedgerSummary(
    int Count, int Pending, int Completed, int Failed, int Reversed);
public sealed record AdminAiTransactionSummary(
    int Count, decimal Volume, int Completed, int Pending, int Failed, decimal SuccessRate,
    AdminAiLedgerSummary Ledger);
public sealed record AdminAiSupportSummary(int Awaiting, int NewCases, int WaitingForUser);
public sealed record AdminAiDisputeSummary(int Open, int Investigating);
public sealed record AdminAiCaseSummary(
    AdminAiSupportSummary Support, AdminAiDisputeSummary Disputes, int PendingRiskReviews);
public sealed record AdminAiDirectPaymentSummary(
    int Pending, int AwaitingReconciliation, int CompletedInWindow, int FailedInWindow);
public sealed record AdminAiReconciliationSummary(int UnmatchedInWindow, int FailedInWindow);
public sealed record AdminAiBankProviderSummary(bool Enabled, bool Configured, string Environment);
public sealed record AdminAiBankingSummary(
    AdminAiDirectPaymentSummary DirectPayments,
    AdminAiReconciliationSummary Reconciliation,
    AdminAiBankProviderSummary Provider);
public sealed record AdminAiOutboxSummary(int Pending, int Failed);
public sealed record AdminAiSystemSummary(
    string Database, string AiProvider, bool MaintenanceEnabled,
    AdminAiOutboxSummary NotificationsOutbox, int AuditEventsInWindow);
public sealed record AdminAiContext(
    DateTime GeneratedAtUtc,
    DateTime WindowStartUtc,
    int PeriodDays,
    AdminAiUserSummary Users,
    AdminAiTransactionSummary Transactions,
    AdminAiCaseSummary Cases,
    AdminAiBankingSummary Banking,
    AdminAiSystemSummary System);

// Chỉ tổng hợp số đếm/trạng thái đã duyệt; không đưa bản ghi cá nhân hay bí mật cho model.
public sealed class AdminAiContextBuilder(
    ISpendWiseDbContext db,
    IDatabaseHealthChecker healthChecker,
    IAiProviderStatus aiProviderStatus,
    IBankHubConfiguration bankConfiguration,
    IClock clock)
{
    public async Task<AdminAiContext> BuildAsync(int periodDays, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var start = now.AddDays(-periodDays);
        var transactions = db.FinancialTransactions.AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= now);
        var ledger = db.LedgerTransactions.AsNoTracking()
            .Where(item => item.CreatedAt >= start && item.CreatedAt <= now);
        var transactionCount = await transactions.CountAsync(cancellationToken);
        var completed = await transactions.CountAsync(
            item => item.Status == FinancialTransactionStatus.Completed, cancellationToken);
        var bank = bankConfiguration.GetSafeStatus();

        return new AdminAiContext(
            now,
            start,
            periodDays,
            new AdminAiUserSummary(
                await db.Users.CountAsync(cancellationToken),
                await db.Users.CountAsync(x => x.Status == UserStatus.Active, cancellationToken),
                await db.Users.CountAsync(x => x.Status == UserStatus.Restricted, cancellationToken),
                await db.Users.CountAsync(x => x.Status == UserStatus.Locked, cancellationToken),
                await db.Users.CountAsync(x => x.Status == UserStatus.Suspended, cancellationToken),
                await db.Users.CountAsync(x => x.CreatedAt >= start, cancellationToken)),
            new AdminAiTransactionSummary(
                transactionCount,
                await transactions.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0,
                completed,
                await transactions.CountAsync(x => x.Status == FinancialTransactionStatus.Pending
                    || x.Status == FinancialTransactionStatus.Processing, cancellationToken),
                await transactions.CountAsync(x => x.Status == FinancialTransactionStatus.Failed, cancellationToken),
                transactionCount == 0 ? 100 : Math.Round((decimal)completed / transactionCount * 100, 2),
                new AdminAiLedgerSummary(
                    await ledger.CountAsync(cancellationToken),
                    await ledger.CountAsync(x => x.Status == LedgerTransactionStatus.Pending, cancellationToken),
                    await ledger.CountAsync(x => x.Status == LedgerTransactionStatus.Completed, cancellationToken),
                    await ledger.CountAsync(x => x.Status == LedgerTransactionStatus.Failed, cancellationToken),
                    await ledger.CountAsync(x => x.Status == LedgerTransactionStatus.Reversed, cancellationToken))),
            new AdminAiCaseSummary(
                new AdminAiSupportSummary(
                    await db.SupportCases.CountAsync(x => x.Status == CaseStatus.New
                        || x.Status == CaseStatus.Open || x.Status == CaseStatus.Investigating
                        || x.Status == CaseStatus.WaitingForUser, cancellationToken),
                    await db.SupportCases.CountAsync(x => x.Status == CaseStatus.New, cancellationToken),
                    await db.SupportCases.CountAsync(x => x.Status == CaseStatus.WaitingForUser, cancellationToken)),
                new AdminAiDisputeSummary(
                    await db.Disputes.CountAsync(x => x.Status != CaseStatus.Closed
                        && x.Status != CaseStatus.Resolved, cancellationToken),
                    await db.Disputes.CountAsync(x => x.Status == CaseStatus.Investigating, cancellationToken)),
                await db.RiskCases.CountAsync(x => x.ReviewStatus == ReviewStatus.Pending, cancellationToken)),
            new AdminAiBankingSummary(
                new AdminAiDirectPaymentSummary(
                    await db.BankDirectPayments.CountAsync(x => x.Status == BankDirectPaymentStatus.Pending, cancellationToken),
                    await db.BankDirectPayments.CountAsync(
                        x => x.Status == BankDirectPaymentStatus.AwaitingReconciliation, cancellationToken),
                    await db.BankDirectPayments.CountAsync(x => x.Status == BankDirectPaymentStatus.Completed
                        && x.CompletedAt >= start && x.CompletedAt <= now, cancellationToken),
                    await db.BankDirectPayments.CountAsync(x => x.Status == BankDirectPaymentStatus.Failed
                        && x.CreatedAt >= start && x.CreatedAt <= now, cancellationToken)),
                new AdminAiReconciliationSummary(
                    await db.BankIntegrationEvents.CountAsync(x => x.ProcessingStatus == BankIntegrationEventStatus.Unmatched
                        && x.CreatedAt >= start && x.CreatedAt <= now, cancellationToken),
                    await db.BankIntegrationEvents.CountAsync(x => x.ProcessingStatus == BankIntegrationEventStatus.Failed
                        && x.CreatedAt >= start && x.CreatedAt <= now, cancellationToken)),
                new AdminAiBankProviderSummary(bank.Enabled, bank.Configured, bank.Environment)),
            new AdminAiSystemSummary(
                await healthChecker.CanConnectAsync(cancellationToken) ? "Healthy" : "Unhealthy",
                aiProviderStatus.Status,
                await db.SystemMaintenanceStates.AsNoTracking()
                    .OrderByDescending(x => x.ChangedAt).Select(x => x.IsEnabled).FirstOrDefaultAsync(cancellationToken),
                new AdminAiOutboxSummary(
                    await db.OutboxMessages.CountAsync(x => x.ProcessedAt == null, cancellationToken),
                    await db.OutboxMessages.CountAsync(x => x.LastError != null, cancellationToken)),
                await db.AuditLogs.CountAsync(x => x.CreatedAt >= start && x.CreatedAt <= now, cancellationToken)));
    }
}
