using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SpendWise.API.Infrastructure;
using SpendWise.Application.Banking;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Banking;

namespace SpendWise.API.Controllers;

// Read-only, aggregate-only finance endpoints for the single ordinary Admin in V1.
// No User financial record, account identifier, provider secret or raw payload is returned.
public sealed record AdminFinanceStatusTotal(string Status, int Count, decimal AmountVnd);
public sealed record AdminFinanceFlow(
    int Total, int Completed, decimal CompletedAmountVnd,
    IReadOnlyList<AdminFinanceStatusTotal> ByStatus);
public sealed record AdminBankConnectionCount(string AccountEnvironment, string Status, int Count);
public sealed record AdminFinanceOverview(
    DateTime FromUtc, DateTime ToUtc,
    AdminFinanceFlow TopUps, AdminFinanceFlow Withdrawals,
    IReadOnlyList<AdminBankConnectionCount> BankConnections);
public sealed record AdminSePayEventCount(string Status, int Count);
public sealed record AdminSePayHealth(
    DateTime CheckedAtUtc,
    SePayBankHubStatusDto Configuration,
    string ProviderCallEvidence,
    string WebhookEvidence,
    DateTime? LatestPersistedWebhookAtUtc,
    DateTime? LatestTestModeTransactionAtUtc,
    int ActiveTestModeConnections,
    IReadOnlyList<AdminSePayEventCount> WebhookEventsLast24Hours,
    bool LiveProviderProbePerformed);

[ApiController]
[Authorize(Policy = V1Authorization.OperationsRead)]
[Route("api/v1/admin/finance")]
public sealed class AdminFinanceObservabilityController(
    ISpendWiseDbContext db,
    IBankHubConfiguration bankHub) : ControllerBase
{
    // Counts and VND amounts come from requests stored by SpendWise, not
    // mirrored FinancialTransaction history or assumed bank balances.
    [HttpGet("overview")]
    public async Task<AdminFinanceOverview> Overview(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 90)
            throw new AppException("invalid_finance_window", "Choose 1 to 90 days.");

        var end = DateTime.UtcNow;
        var start = end.AddDays(-days);

        var topUps = await db.WalletTopUps.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var withdrawals = await db.WalletWithdrawals.AsNoTracking()
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);
        var connections = await db.BankConnections.AsNoTracking()
            .GroupBy(x => new { x.AccountEnvironment, x.Status })
            .Select(g => new { g.Key.AccountEnvironment, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var topUpFlow = new AdminFinanceFlow(
            topUps.Sum(x => x.Count),
            topUps.Where(x => x.Status == WalletTopUpStatus.Completed).Sum(x => x.Count),
            topUps.Where(x => x.Status == WalletTopUpStatus.Completed).Sum(x => x.Amount),
            topUps.Select(x => new AdminFinanceStatusTotal(x.Status.ToString(), x.Count, x.Amount))
                .OrderBy(x => x.Status).ToList());
        var withdrawalFlow = new AdminFinanceFlow(
            withdrawals.Sum(x => x.Count),
            withdrawals.Where(x => x.Status == WalletWithdrawalStatus.Completed).Sum(x => x.Count),
            withdrawals.Where(x => x.Status == WalletWithdrawalStatus.Completed).Sum(x => x.Amount),
            withdrawals.Select(x => new AdminFinanceStatusTotal(x.Status.ToString(), x.Count, x.Amount))
                .OrderBy(x => x.Status).ToList());

        return new AdminFinanceOverview(
            start, end, topUpFlow, withdrawalFlow,
            connections.Select(x => new AdminBankConnectionCount(
                x.AccountEnvironment.ToString(), x.Status.ToString(), x.Count))
                .OrderBy(x => x.AccountEnvironment).ThenBy(x => x.Status).ToList());
    }

    // Passive observations: never describe a configured provider as "online".
    // No provider request, webhook invocation or sandbox financial operation happens here.
    [HttpGet("sepay/health")]
    public async Task<AdminSePayHealth> SePayHealth(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since = now.AddHours(-24);
        var config = bankHub.GetSafeStatus();
        var events = db.BankIntegrationEvents.AsNoTracking()
            .Where(x => x.Provider == BankProvider.SePayBankHub);
        var latestEvent = await events.Select(x => (DateTime?)x.ReceivedAt)
            .MaxAsync(cancellationToken);
        var recent = await events.Where(x => x.ReceivedAt >= since)
            .GroupBy(x => x.ProcessingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var latestTestModeTransaction = await db.BankTransactions.AsNoTracking()
            .Where(x => x.BankConnection.AccountEnvironment == SePayAccountEnvironment.TestMode)
            .Select(x => (DateTime?)x.ImportedAt).MaxAsync(cancellationToken);
        var testModeConnections = await db.BankConnections.AsNoTracking()
            .CountAsync(x => x.AccountEnvironment == SePayAccountEnvironment.TestMode
                && x.Status == BankConnectionStatus.Active, cancellationToken);

        // Time evidence is process-local and can disappear on restart; unknown means
        // unknown, never a verified provider outage or success.
        var providerEvidence = !config.Enabled ? "Disabled"
            : !config.Configured ? "NotConfigured"
            : IsRecent(config.LastSuccessfulProviderCallAt, now) ? "RecentSuccessfulCall"
            : "NotVerified";
        var webhookEvidence = !config.LifecycleWebhookConfigured && !config.IpnConfigured
            ? "NotConfigured"
            : IsRecent(latestEvent, now) ? "RecentPersistedEvent"
            : "NoRecentEvent";

        return new AdminSePayHealth(
            now, config, providerEvidence, webhookEvidence,
            latestEvent, latestTestModeTransaction, testModeConnections,
            recent.Select(x => new AdminSePayEventCount(x.Status.ToString(), x.Count))
                .OrderBy(x => x.Status).ToList(),
            LiveProviderProbePerformed: false);
    }

    private static bool IsRecent(DateTime? at, DateTime now) =>
        at is { } timestamp && timestamp <= now && timestamp >= now.AddHours(-24);
}
