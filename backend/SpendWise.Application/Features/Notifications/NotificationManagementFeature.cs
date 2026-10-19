using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Notifications;
using SpendWise.Application.Features.Budgets;
using SpendWise.Domain.Notifications;

namespace SpendWise.Application.Features.Notifications;

public sealed record SearchNotificationsQuery(Guid UserId, NotificationSearchFilter Filter) : IRequest<NotificationPageDto>;
public sealed record DeleteNotificationCommand(Guid UserId, Guid NotificationId) : IRequest;
public sealed record ClearNotificationsCommand(Guid UserId) : IRequest<int>;
public sealed record GetNotificationPreferencesQuery(Guid UserId) : IRequest<IReadOnlyList<NotificationPreferenceDto>>;
public sealed record SaveNotificationPreferenceCommand(Guid UserId, NotificationPreferenceRequest Request) : IRequest<NotificationPreferenceDto>;

public sealed class NotificationManagementHandler(ISpendWiseDbContext db) :
    IRequestHandler<SearchNotificationsQuery, NotificationPageDto>, IRequestHandler<DeleteNotificationCommand>,
    IRequestHandler<ClearNotificationsCommand, int>, IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>,
    IRequestHandler<SaveNotificationPreferenceCommand, NotificationPreferenceDto>
{
    public async Task<NotificationPageDto> Handle(SearchNotificationsQuery query, CancellationToken ct)
    {
        InputBoundary.Page(query.Filter.Page, query.Filter.PageSize);
        var source = db.Notifications.AsNoTracking().Where(x => x.UserId == query.UserId);
        if (query.Filter.UnreadOnly == true) source = source.Where(x => x.ReadAt == null);
        if (!string.IsNullOrWhiteSpace(query.Filter.Type)) source = source.Where(x => x.Type.ToString() == query.Filter.Type);
        if (!string.IsNullOrWhiteSpace(query.Filter.Search)) { var search = query.Filter.Search.Trim(); source = source.Where(x => x.Title.Contains(search) || x.Message.Contains(search)); }
        var total = await source.CountAsync(ct);
        var items = await source.OrderByDescending(x => x.CreatedAt).Skip((query.Filter.Page - 1) * query.Filter.PageSize).Take(query.Filter.PageSize).ToListAsync(ct);
        return new(items.Select(NotificationMappings.ToDto).ToList(), query.Filter.Page, query.Filter.PageSize, total);
    }
    public async Task Handle(DeleteNotificationCommand command, CancellationToken ct) { var item = await db.Notifications.SingleOrDefaultAsync(x => x.Id == command.NotificationId && x.UserId == command.UserId, ct) ?? throw new NotFoundException("notification_not_found", "Notification not found."); db.Notifications.Remove(item); await db.SaveChangesAsync(ct); }
    public async Task<int> Handle(ClearNotificationsCommand command, CancellationToken ct) { var count = await db.Notifications.Where(x => x.UserId == command.UserId).ExecuteDeleteAsync(ct); return count; }
    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(GetNotificationPreferencesQuery query, CancellationToken ct) => await db.NotificationPreferences.AsNoTracking().Where(x => x.UserId == query.UserId).OrderBy(x => x.AccountName).Select(x => Map(x)).ToListAsync(ct);
    public async Task<NotificationPreferenceDto> Handle(SaveNotificationPreferenceCommand command, CancellationToken ct)
    {
        if (command.Request.LargeTransactionThreshold < 0 || command.Request.MinBalanceThreshold < 0) throw new AppException("invalid_threshold", "Notification thresholds cannot be negative.");
        var key = InputBoundary.RequiredText(command.Request.AccountKey, 100, "Account key");
        var name = InputBoundary.RequiredText(command.Request.AccountName, 160, "Account name");
        if (command.Request.WalletId is Guid walletId && !await db.Wallets.AnyAsync(x => x.Id == walletId && x.OwnerUserId == command.UserId, ct)) throw new NotFoundException("wallet_not_found", "Wallet not found.");
        if (command.Request.WalletId is null && key is not ("all" or "business")) throw new AppException("invalid_account_scope", "Virtual account scope is invalid.");
        var item = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == command.UserId && x.AccountKey == key, ct);
        if (item is null) { item = new NotificationPreference { UserId = command.UserId, AccountKey = key }; db.NotificationPreferences.Add(item); }
        item.AccountName = name; item.WalletId = command.Request.WalletId; item.NotifyBalanceChanges = command.Request.NotifyBalanceChanges; item.NotifyBudgetAlerts = command.Request.NotifyBudgetAlerts; item.NotifyLargeTransactions = command.Request.NotifyLargeTransactions; item.LargeTransactionThreshold = command.Request.LargeTransactionThreshold; item.NotifyMinBalanceAlert = command.Request.NotifyMinBalanceAlert; item.MinBalanceThreshold = command.Request.MinBalanceThreshold;
        await db.SaveChangesAsync(ct); return Map(item);
    }
    private static NotificationPreferenceDto Map(NotificationPreference x) => new(x.Id, x.AccountKey, x.AccountName, x.WalletId, x.NotifyBalanceChanges, x.NotifyBudgetAlerts, x.NotifyLargeTransactions, x.LargeTransactionThreshold, x.NotifyMinBalanceAlert, x.MinBalanceThreshold);
}

public sealed class BudgetAlertEvaluator(
    ISpendWiseDbContext db,
    INotificationService notifications,
    IClock clock,
    BudgetReader budgetReader)
{
    public async Task EvaluateAsync(Guid userId, CancellationToken ct)
    {
        var localNow = VietnamBusinessTime.ToLocal(clock.UtcNow);
        var enabled = !await db.NotificationPreferences.AsNoTracking().AnyAsync(
            x => x.UserId == userId && x.AccountKey == "all" && !x.NotifyBudgetAlerts, ct);
        if (!enabled) return;
        var budgets = await db.Budgets.AsNoTracking().Where(x => x.OwnerUserId == userId
            && x.Year == localNow.Year && x.Month == localNow.Month && x.AlertsEnabled).ToListAsync(ct);
        foreach (var budget in budgets)
        {
            var calculation = await budgetReader.CalculateAsync(
                userId, budget.CategoryId, budget.Year, budget.Month, ct);
            var threshold = calculation.SpentAmount >= calculation.EffectiveLimitAmount
                ? 100
                : calculation.SpentAmount >= calculation.EffectiveLimitAmount * .9m ? 90 : 0;
            if (threshold == 0) continue;
            var key = $"budget:{budget.Id}:{budget.Year:D4}{budget.Month:D2}:{threshold}";
            if (await db.NotificationDeduplications.AnyAsync(x => x.UserId == userId && x.Key == key, ct)) continue;
            var notification = await notifications.CreateAsync(userId, threshold == 100 ? "BudgetExceeded" : "BudgetWarning", threshold == 100 ? "Budget exceeded" : "Budget warning", threshold == 100 ? "Budget usage reached 100%." : "Budget usage reached 90%.", new { budgetId = budget.Id, threshold, actionRoute = $"/budgets/{budget.Id}", actionLabelKey = "budget.view" }, ct);
            db.NotificationDeduplications.Add(new NotificationDeduplication { UserId = userId, Key = key, NotificationId = notification.Id }); await db.SaveChangesAsync(ct);
        }
    }
}

public sealed class FinancialAlertEvaluator(
    ISpendWiseDbContext db,
    INotificationService notifications,
    IClock clock)
{
    public async Task EvaluateAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preferences = await db.NotificationPreferences.AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);
        if (preferences.Count == 0) return;

        var transactions = await db.FinancialTransactions.AsNoTracking()
            .Where(item => item.OwnerUserId == userId
                && item.Status == SpendWise.Domain.Finance.FinancialTransactionStatus.Completed
                && item.CreatedAt >= clock.UtcNow.AddDays(-30))
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            var preference = preferences.FirstOrDefault(item => item.WalletId == transaction.WalletId)
                ?? preferences.FirstOrDefault(item => item.AccountKey == "all");
            if (preference is null) continue;

            if (preference.NotifyBalanceChanges)
            {
                await CreateOnceAsync(userId, $"balance:{transaction.Id}", "BalanceChanged",
                    "Balance changed", "A completed transaction changed your wallet balance.",
                    new { transactionId = transaction.Id, walletId = transaction.WalletId,
                        actionRoute = $"/transactions/{transaction.Id}", actionLabelKey = "transaction.view" }, cancellationToken);
            }
            if (preference.NotifyLargeTransactions && preference.LargeTransactionThreshold > 0
                && transaction.Amount >= preference.LargeTransactionThreshold)
            {
                await CreateOnceAsync(userId, $"large:{transaction.Id}:{preference.LargeTransactionThreshold}",
                    "LargeTransaction", "Large transaction", "A transaction reached your configured threshold.",
                    new { transactionId = transaction.Id, walletId = transaction.WalletId,
                        threshold = preference.LargeTransactionThreshold, actionRoute = $"/transactions/{transaction.Id}",
                        actionLabelKey = "transaction.view" }, cancellationToken);
            }
        }

        foreach (var preference in preferences.Where(item => item.NotifyMinBalanceAlert
                     && item.MinBalanceThreshold > 0 && item.WalletId.HasValue))
        {
            var balance = await db.Wallets.AsNoTracking()
                .Where(wallet => wallet.Id == preference.WalletId && wallet.OwnerUserId == userId)
                .Select(wallet => (decimal?)wallet.AvailableBalance)
                .SingleOrDefaultAsync(cancellationToken);
            if (balance is not null && balance <= preference.MinBalanceThreshold)
            {
                await CreateOnceAsync(userId, $"minimum:{preference.WalletId}:{preference.MinBalanceThreshold}",
                    "MinimumBalance", "Minimum balance", "A wallet reached your configured minimum balance.",
                    new { walletId = preference.WalletId, threshold = preference.MinBalanceThreshold,
                        actionRoute = $"/wallets/{preference.WalletId}", actionLabelKey = "wallet.view" }, cancellationToken);
            }
        }
    }

    private async Task CreateOnceAsync(Guid userId, string key, string type, string title, string message,
        object data, CancellationToken cancellationToken)
    {
        if (await db.NotificationDeduplications.AnyAsync(item => item.UserId == userId && item.Key == key,
                cancellationToken)) return;
        var notification = await notifications.CreateAsync(userId, type, title, message, data, cancellationToken);
        db.NotificationDeduplications.Add(new NotificationDeduplication
        {
            UserId = userId,
            Key = key,
            NotificationId = notification.Id
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
