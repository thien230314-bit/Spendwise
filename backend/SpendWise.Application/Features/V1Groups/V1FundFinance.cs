using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1ConfirmObligationsRequest(long TargetVnd, string SecurityPin);
public sealed record V1ContributeRequest(Guid WalletId, long AmountVnd, string IdempotencyKey, string SecurityPin, long? ObligationRevision = null, string? Purpose = null);
public sealed record V1ContributionDto(Guid Id, Guid FundId, Guid UserId, Guid WalletId, long AmountVnd, long? ObligationRevision, Guid LedgerTransactionId, DateTime CreatedAt, bool Replayed);
public sealed record V1ObligationDto(Guid UserId, int Seat, long RequiredVnd, long PaidVnd, long RemainingVnd, long LockedExcessVnd);
public sealed record V1FinanceDto(Guid FundId, long Revision, long? TargetVnd, decimal BalanceVnd, bool ConfigurationCurrent,
    bool ContributionsEnabled, string SpendingState, IReadOnlyList<V1ObligationDto> Obligations);

public sealed partial class V1FundService
{
    private const long MaximumVnd = 9_999_999_999_999_999L;

    public async Task<V1FinanceDto> ConfirmObligationsAsync(Guid ownerId, Guid fundId, V1ConfirmObligationsRequest request, CancellationToken ct)
    {
        if (request.TargetVnd is <= 0 or > MaximumVnd) throw new AppException("invalid_amount", "Use positive integer VND within the supported range.");
        await RequireActorAsync(ownerId, ct); await security.VerifyAsync(ownerId, request.SecurityPin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([ownerId], token);
            var fund = await LockFundAsync(fundId, token); RequireOwner(fund, ownerId);
            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Closed activity obligations cannot change.");
            if (fund.Kind != V1FundKind.Fixed) throw new ConflictException("fixed_fund_required", "Free funds have no fixed obligations.");
            var treasury = await TreasuryAsync(fund, token);
            await RequireUnspentAccountingAsync(treasury, token);
            var members = await ActiveMembersAsync(fundId, token);
            var signature = MembershipSignature(members);
            if (treasury.Revision == 0 || treasury.TargetVnd != request.TargetVnd || treasury.MembershipSignature != signature)
            {
                treasury.Revision = checked(treasury.Revision + 1);
                treasury.TargetVnd = request.TargetVnd; treasury.MembershipSignature = signature;
                var amounts = FixedFundAllocation.Allocate(request.TargetVnd, members.Select(m => m.Seat).ToArray());
                db.V1FundObligations.AddRange(members.Select(m => new V1FundObligation { FundId = fundId, Revision = treasury.Revision,
                    MembershipId = m.Id, UserId = m.UserId, Seat = m.Seat, RequiredVnd = amounts[m.Seat] }));
                await db.V1Funds.Where(f => f.Id == fundId).ExecuteUpdateAsync(s => s.SetProperty(f => f.TargetVnd, (long?)request.TargetVnd), token);
                await ActivityAsync(fundId, ownerId, "ObligationsConfirmed", token, detail: treasury.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return await FinanceStateAsync(await FundAsync(fundId, token), treasury, token);
        }, ct);
    }

    public Task<V1FinanceDto> FinanceAsync(Guid userId, Guid fundId, CancellationToken ct) => db.ExecuteInTransactionAsync(async token =>
    {
        await RequireActorAsync(userId, token); var fund = await LockFundAsync(fundId, token);
        await RequireMemberAsync(userId, fundId, token);
        var treasury = await db.V1FundTreasuries.AsNoTracking().SingleOrDefaultAsync(t => t.FundId == fundId, token);
        if (treasury is null) return new V1FinanceDto(fundId, 0, fund.TargetVnd, 0,
            fund.Kind == V1FundKind.Free, fund.Kind == V1FundKind.Free && fund.ActivityClosedAt is null,
            fund.ActivityClosedAt is not null ? "ACTIVITY_CLOSED" : fund.DissolvedAt is not null ? "DISSOLVED" : (fund.Kind == V1FundKind.Free ? "OPEN" : "FINANCE_BLOCKED"), []);
        await FreshAccountAsync(treasury.LedgerAccountId, token);
        await RequireReconciledAccountingAsync(treasury, token);
        return await FinanceStateAsync(fund, treasury, token);
    }, ct);

    private async Task<V1FundTreasury> TreasuryAsync(V1Fund fund, CancellationToken ct)
    {
        var treasury = await db.V1FundTreasuries.SingleOrDefaultAsync(t => t.FundId == fund.Id, ct);
        if (treasury is null)
        {
            var account = new LedgerAccount { Code = "V1-FUND:" + fund.Id.ToString("N"), Name = "V1 Fund Treasury", Type = LedgerAccountType.SharedFund };
            treasury = new V1FundTreasury { FundId = fund.Id, LedgerAccountId = account.Id };
            db.LedgerAccounts.Add(account); db.V1FundTreasuries.Add(treasury); await db.SaveChangesAsync(ct);
        }
        else if (db is DbContext context && context.Entry(treasury).State == EntityState.Unchanged) await context.Entry(treasury).ReloadAsync(ct);
        await FreshAccountAsync(treasury.LedgerAccountId, ct);
        return treasury;
    }

    private async Task<LedgerAccount> FreshAccountAsync(Guid accountId, CancellationToken ct)
    {
        var account = await db.LedgerAccounts.SingleAsync(a => a.Id == accountId, ct);
        if (db is DbContext context)
        {
            if (context.Entry(account).State != EntityState.Unchanged) throw new ConflictException("pending_financial_state", "Pending account changes cannot be posted.");
            await context.Entry(account).ReloadAsync(ct);
        }
        return account;
    }

    private Task<List<V1FundMembership>> ActiveMembersAsync(Guid id, CancellationToken ct) => db.V1FundMemberships.AsNoTracking()
        .Where(m => m.FundId == id && m.LeftAt == null).OrderBy(m => m.Seat).ToListAsync(ct);
    private static string MembershipSignature(IReadOnlyCollection<V1FundMembership> members) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", members.OrderBy(m => m.Seat).Select(m => $"{m.Id:N}:{m.UserId:N}:{m.Seat}")))));

    private async Task RequireUnspentAccountingAsync(V1FundTreasury treasury, CancellationToken ct)
    {
        var payments = await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == treasury.FundId).ToListAsync(ct);
        var paid = payments.Sum(c => c.AmountVnd);
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerAccountId == account.Id)
            .Select(e => new { e.LedgerTransactionId, e.Direction, e.Amount, e.Currency }).ToListAsync(ct);
        var net = entries.Sum(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount);
        var postings = await db.LedgerTransactions.AsNoTracking().Include(t => t.Entries)
            .Where(t => db.V1FundContributions.Any(c => c.FundId == treasury.FundId && c.LedgerTransactionId == t.Id)).ToListAsync(ct);
        var byId = postings.ToDictionary(t => t.Id);
        var postingValid = payments.All(c => byId.TryGetValue(c.LedgerTransactionId, out var t) && t.BusinessId == c.Id
            && t.BusinessType == "V1FundContribution" && t.Status == LedgerTransactionStatus.Completed
            && t.Entries.Count == 2 && t.Entries.All(e => e.Currency == "VND" && e.Amount == c.AmountVnd)
            && t.Entries.Count(e => e.Direction == LedgerEntryDirection.Debit && e.LedgerAccountId == account.Id) == 1
            && t.Entries.Count(e => e.Direction == LedgerEntryDirection.Credit) == 1);
        // Net ledger reconciliation can account for future outflow; contribution-only eligibility still refuses any Credit.
        if (account.Currency != "VND" || account.Balance != paid || net != account.Balance || !postingValid
            || entries.Any(e => e.Direction != LedgerEntryDirection.Debit || e.Currency != "VND" || e.Amount <= 0))
            throw new ConflictException("FINANCE_BLOCKED", "Fund requires reconciliation before this operation.");
    }

    private async Task RequireReconciledAccountingAsync(V1FundTreasury treasury, CancellationToken ct)
    {
        var payments = await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == treasury.FundId).ToListAsync(ct);
        var account = await db.LedgerAccounts.AsNoTracking().SingleAsync(a => a.Id == treasury.LedgerAccountId, ct);
        var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerAccountId == account.Id)
            .Select(e => new { e.LedgerTransactionId, e.Direction, e.Amount, e.Currency }).ToListAsync(ct);
        var net = entries.Sum(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount);

        if (account.Currency != "VND" || net != account.Balance)
            throw new ConflictException("FINANCE_BLOCKED", "Fund ledger requires reconciliation before this operation.");

        var postings = await db.LedgerTransactions.AsNoTracking().Include(t => t.Entries)
            .Where(t => db.V1FundContributions.Any(c => c.FundId == treasury.FundId && c.LedgerTransactionId == t.Id)).ToListAsync(ct);
        var byId = postings.ToDictionary(t => t.Id);
        var contributionValid = payments.All(c => byId.TryGetValue(c.LedgerTransactionId, out var t) && t.BusinessId == c.Id
            && t.BusinessType == "V1FundContribution" && t.Status == LedgerTransactionStatus.Completed
            && t.Entries.Count == 2 && t.Entries.All(e => e.Currency == "VND" && e.Amount == c.AmountVnd)
            && t.Entries.Count(e => e.Direction == LedgerEntryDirection.Debit && e.LedgerAccountId == account.Id) == 1
            && t.Entries.Count(e => e.Direction == LedgerEntryDirection.Credit) == 1);
        if (!contributionValid)
            throw new ConflictException("FINANCE_BLOCKED", "Fund contributions have invalid ledger postings.");

        var creditEntries = entries.Where(e => e.Direction == LedgerEntryDirection.Credit).ToList();
        if (creditEntries.Count > 0)
        {
            var creditPostings = await db.LedgerTransactions.AsNoTracking()
                .Where(t => creditEntries.Select(e => e.LedgerTransactionId).Contains(t.Id))
                .ToListAsync(ct);
            if (creditPostings.Count != creditEntries.Count || creditPostings.Any(t => t.Status != LedgerTransactionStatus.Completed))
                throw new ConflictException("FINANCE_BLOCKED", "Fund outflows have incomplete ledger transactions.");
        }
    }

    private async Task<V1FinanceDto> FinanceStateAsync(V1Fund fund, V1FundTreasury treasury, CancellationToken ct)
    {
        var members = await ActiveMembersAsync(fund.Id, ct);
        var current = fund.Kind == V1FundKind.Free || (treasury.Revision > 0 && treasury.TargetVnd == fund.TargetVnd && treasury.MembershipSignature == MembershipSignature(members));
        var paid = (await db.V1FundContributions.AsNoTracking().Where(c => c.FundId == fund.Id).ToListAsync(ct))
            .GroupBy(c => c.MembershipId).ToDictionary(g => g.Key, g => g.Sum(c => c.AmountVnd));
        var obligations = await db.V1FundObligations.AsNoTracking().Where(o => o.FundId == fund.Id && o.Revision == treasury.Revision).OrderBy(o => o.Seat).ToListAsync(ct);
        var rows = obligations.Select(o => new V1ObligationDto(o.UserId, o.Seat, o.RequiredVnd, paid.GetValueOrDefault(o.MembershipId),
            Math.Max(0, o.RequiredVnd - paid.GetValueOrDefault(o.MembershipId)), Math.Max(0, paid.GetValueOrDefault(o.MembershipId) - o.RequiredVnd))).ToList();
        var balance = await db.LedgerAccounts.AsNoTracking().Where(a => a.Id == treasury.LedgerAccountId).Select(a => a.Balance).SingleAsync(ct);
        var spendingState = fund.DissolvedAt is not null
            ? "DISSOLVED"
            : fund.ActivityClosedAt is not null
                ? "ACTIVITY_CLOSED"
                : (fund.Kind == V1FundKind.Free || (current && rows.Count > 0 && rows.All(o => o.RemainingVnd == 0)))
                    ? "OPEN"
                    : "FINANCE_BLOCKED";
        return new(fund.Id, treasury.Revision, treasury.TargetVnd, balance, current, current && fund.ActivityClosedAt is null && fund.DissolvedAt is null, spendingState, rows);
    }
}
