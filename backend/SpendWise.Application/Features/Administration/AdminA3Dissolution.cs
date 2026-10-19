using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Administration;

public sealed record A3Dissolution(string Status, bool CountsAsActiveGroup, string? Code, Guid? FounderUserId, Guid? LastOwnerUserId,
    DateTime? DissolvedAt, Guid? DissolutionRecordId, string DissolutionProvenance, string RefundVerification, int RefundRecords, int VerifiedRefunds,
    string FinancialHistoryEndpoint, string ActivityEndpoint, string LedgerZeroVerification)
{
    public decimal? CompletedLedgerNet { get; init; }
    public decimal? StoredLedgerBalance { get; init; }
}

public sealed partial class AdminA3Service
{
    public Task<A3Dissolution> DissolutionAsync(ClaimsPrincipal principal, Guid caseId, Guid fundId, string reason, CancellationToken ct) =>
        WithFund(principal, caseId, fundId, reason, "DISSOLUTION", async (_, fund, token) =>
        {
            var root = $"/api/v1/admin/support/{caseId}/funds/{fundId}";
            var treasury = await ReadTreasuryAsync(fundId, fund.Code != null, token);
            if (fund.Code != null)
            {
                var actual = await db.V1Funds.AsNoTracking().SingleAsync(f => f.Id == fundId, token);
                return new A3Dissolution(fund.Status, actual.DissolvedAt == null, fund.Code, actual.FounderUserId, actual.OwnerUserId,
                    actual.DissolvedAt is {} at ? Utc(at) : null, null, actual.DissolvedAt == null ? "NOT_RECORDED" : "V1Fund.DissolvedAt",
                    "NOT_VERIFIED; V1 refund execution NOT_IMPLEMENTED", 0, 0, root + "/finance/Contributions", root + "/activity", treasury.ZeroLedgerEvidence ? "VERIFIED_NET_ZERO" : "NOT_VERIFIED")
                    { CompletedLedgerNet = treasury.CompletedLedgerNet, StoredLedgerBalance = treasury.StoredBalance };
            }
            var deletion = await db.GroupDeletionRequests.AsNoTracking().Where(d => d.GroupId == fundId)
                .OrderByDescending(d => d.CompletedAt != null).ThenByDescending(d => d.CompletedAt).ThenByDescending(d => d.CreatedAt).ThenBy(d => d.Id).FirstOrDefaultAsync(token);
            var owner = await db.GroupMembers.AsNoTracking().Where(m => m.GroupId == fundId && m.Role == GroupRole.Owner).Select(m => (Guid?)m.UserId).SingleOrDefaultAsync(token);
            var count = deletion == null ? 0 : await db.GroupRefunds.CountAsync(r => r.GroupDeletionRequestId == deletion.Id, token);
            var currency = await db.Groups.Where(g => g.Id == fundId).Select(g => g.Currency).SingleAsync(token);
            var verified = 0; decimal total = 0; var allCompleted = true; var refundUsers = new HashSet<Guid>();
            // Process historical refunds in bounded pages; no member share or entitlement formula is recomputed.
            for (var offset = 0; offset < count; offset += 100)
            {
                var refunds = await db.GroupRefunds.AsNoTracking().Where(r => r.GroupDeletionRequestId == deletion!.Id).OrderBy(r => r.Id).Skip(offset).Take(100).ToListAsync(token);
                var money = refunds.Where(r => r.RefundAmount > 0).Select(r => new A3Money { Id = r.Id, Kind = "Refund", Amount = r.RefundAmount, Currency = currency, UserId = r.UserId,
                    WalletId = r.DestinationWalletId, LedgerTransactionId = r.LedgerTransactionId, State = r.Status.ToString() }).ToList();
                var proofs = await VerifyMoneyAsync(fundId, false, money, token);
                foreach (var r in refunds) { total += r.RefundAmount; allCompleted &= r.Status == GroupRefundStatus.Completed && r.RefundAmount >= 0 && refundUsers.Add(r.UserId);
                    if (r.RefundAmount == 0 && r.Status == GroupRefundStatus.Completed || proofs.Any(p => p.Id == r.Id && p.Verification == "LEDGER_COMPLETED")) verified++; }
            }
            var expected = deletion == null ? [] : await db.GroupDeletionVotes.Where(v => v.GroupDeletionRequestId == deletion.Id).Select(v => v.UserId).Distinct().ToListAsync(token);
            var complete = deletion != null && deletion.Status == GroupDeletionRequestStatus.Completed && count > 0 && expected.Count > 0 && refundUsers.SetEquals(expected)
                && total == deletion.FundBalanceSnapshot && allCompleted && verified == count;
            return new A3Dissolution(fund.Status, fund.Status != "Dissolved", null, null, owner ?? deletion?.OwnerUserId,
                fund.Status == "Dissolved" && deletion?.Status == GroupDeletionRequestStatus.Completed && deletion.CompletedAt is {} date ? Utc(date) : null,
                deletion?.Id, deletion == null ? "Group.Status; exact event/time NOT_RECORDED" : "GroupDeletionRequest; status/time independent of ledger",
                complete ? total == 0 ? "VERIFIED_NO_REFUND_DUE" : "VERIFIED" : "NOT_VERIFIED", count, verified, root + "/finance/Refunds", root + "/activity", treasury.ZeroLedgerEvidence ? "VERIFIED_NET_ZERO" : "NOT_VERIFIED")
                { CompletedLedgerNet = treasury.CompletedLedgerNet, StoredLedgerBalance = treasury.StoredBalance };
        }, ct);
}
