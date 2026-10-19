using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;

namespace SpendWise.Application.Features.Operations;

public static class V1MaintenanceGate
{
    public static async Task RequireApiAvailableAsync(ISpendWiseDbContext db, DateTime now, string path, CancellationToken ct)
    {
        var scopes = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && p.ClosedAt == null && p.StartsAt <= now).Select(p => p.Scope).Distinct().ToListAsync(ct);
        bool At(string prefix) => path.Equals(prefix, StringComparison.OrdinalIgnoreCase) || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        var groups = At("/api/groups") || At("/api/group-funds") || At("/api/v1/funds");
        var ai = At("/api/ai");
        var money = groups || ai || At("/api/wallets") || At("/api/transactions") || At("/api/transfers") || At("/api/goals")
            || At("/api/bank-connections") || At("/api/bank-transactions") || At("/api/banks") || At("/api/bank-transfers") || At("/api/integrations/sepay");
        if (scopes.Contains("Global") || scopes.Contains("Groups") && (groups || ai) || scopes.Contains("AI") && ai || scopes.Contains("Money") && money)
            throw new AppException("MAINTENANCE_MODE", "The requested module is under maintenance.", 503);
    }
    public static async Task<DateTime> DeadlineAsync(ISpendWiseDbContext db, DateTime start, TimeSpan duration, DateTime observedAt, CancellationToken ct, string scope = "Global")
    {
        var spans = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && (p.Scope == "Global" || p.Scope == scope || scope == "Groups" && p.Scope == "Money") && p.StartsAt <= observedAt
            && (p.ClosedAt == null || p.ClosedAt > start)).OrderBy(p => p.StartsAt).Select(p => new { p.StartsAt, p.ClosedAt }).ToListAsync(ct);
        var cursor = start; var remaining = duration;
        foreach (var span in spans)
        {
            if (span.ClosedAt <= cursor) continue;
            if (span.StartsAt > cursor)
            {
                var active = span.StartsAt - cursor;
                if (remaining <= active) return cursor + remaining;
                remaining -= active; cursor = span.StartsAt;
            }
            if (span.ClosedAt is null) return DateTime.MaxValue;
            if (span.ClosedAt > cursor) cursor = span.ClosedAt.Value;
        }
        return cursor + remaining;
    }
    public static Task<bool> IsActiveAsync(ISpendWiseDbContext db, DateTime now, CancellationToken ct, string scope = "Global") =>
        db.V1MaintenancePlans.AsNoTracking().AnyAsync(p => p.CancelledAt == null && p.ClosedAt == null && p.StartsAt <= now
            && (p.Scope == "Global" || p.Scope == scope || scope == "Groups" && p.Scope == "Money" || scope == "AI" && (p.Scope == "Groups" || p.Scope == "Money")), ct);

    public static async Task RequireAvailableAsync(ISpendWiseDbContext db, DateTime now, CancellationToken ct, string scope = "Global")
    {
        if (await IsActiveAsync(db, now, ct, scope)) throw new AppException("MAINTENANCE_MODE", "The service is under maintenance.", 503);
    }

    public static async Task<TimeSpan> ActiveElapsedAsync(ISpendWiseDbContext db, DateTime start, DateTime end, CancellationToken ct, string scope = "Global")
    {
        if (end <= start) return TimeSpan.Zero;
        var spans = await db.V1MaintenancePlans.AsNoTracking().Where(p => p.CancelledAt == null && (p.Scope == "Global" || p.Scope == scope || scope == "Groups" && p.Scope == "Money") && p.StartsAt < end
                && (p.ClosedAt == null || p.ClosedAt > start)).Select(p => new { p.StartsAt, p.ClosedAt }).ToListAsync(ct);
        long paused = 0; var cursor = start;
        foreach (var span in spans.OrderBy(s => s.StartsAt))
        {
            var a = span.StartsAt > cursor ? span.StartsAt : cursor;
            var b = span.ClosedAt is { } closed && closed < end ? closed : end;
            if (b <= a) continue;
            paused += (b - a).Ticks; cursor = b;
        }
        return TimeSpan.FromTicks((end - start).Ticks - paused);
    }
}
