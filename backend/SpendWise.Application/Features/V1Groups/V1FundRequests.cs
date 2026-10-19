using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

namespace SpendWise.Application.Features.V1Groups;

public sealed record V1FundRequestInput(string Kind, long AmountVnd, Guid RecipientUserId, string Purpose, string IdempotencyKey, string SecurityPin);
public sealed record V1FundRequestDto(Guid Id, Guid FundId, Guid RequestedByUserId, Guid RecipientUserId, string Kind,
    long AmountVnd, string Purpose, string State, Guid? ReviewedByUserId, DateTime? ReviewedAt, string ExecutionAvailability);

public sealed partial class V1FundService
{
    public async Task<V1FundRequestDto> CreateFundRequestAsync(Guid actor, Guid fundId, V1FundRequestInput request, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(request.Purpose, 1000, "Purpose");
        var key = InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key");
        if (request.Kind is not ("Expense" or "Withdrawal") || request.AmountVnd is <= 0 or > MaximumVnd)
            throw new AppException("invalid_fund_request", "Use Expense/Withdrawal and a positive integer VND amount.");
        await RequireActorAsync(actor, ct);
        await security.VerifyAsync(actor, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor, request.RecipientUserId], token);
            var fund = await LockFundAsync(fundId, token);
            await RequireMemberAsync(actor, fundId, token);

            var replay = await db.V1FundRequests.AsNoTracking().SingleOrDefaultAsync(x => x.FundId == fundId && x.RequestedByUserId == actor && x.IdempotencyKey == key, token);
            if (replay is not null)
            {
                if (replay.Kind != request.Kind || replay.AmountVnd != request.AmountVnd || replay.RecipientUserId != request.RecipientUserId || replay.Purpose != purpose)
                    throw new ConflictException("idempotency_conflict", "Request key belongs to different intent.");
                return MapRequest(replay);
            }

            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Activity is closed.");
            if (fund.DissolvedAt is not null) throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");

            if (request.Kind == "Withdrawal" && (fund.Kind != V1FundKind.Free || request.RecipientUserId != actor))
                throw new ForbiddenException("withdrawal_not_allowed", "Free fund members may request only their own withdrawal.");

            var isOwner = actor == fund.OwnerUserId;
            var canSpend = isOwner || await db.V1FundMemberships.AnyAsync(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null && m.CanSpend, token);

            if (request.Kind == "Expense" && !canSpend)
                throw new ForbiddenException("fund_spending_permission_required", "Spending permission is required.");

            await RequireNoActiveElectionAsync(fundId, token);

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);
            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

            if (request.Kind == "Expense")
            {
                // Verify Fixed obligations fulfilled if Fixed fund
                if (fund.Kind == V1FundKind.Fixed)
                {
                    var obligations = await db.V1FundObligations.AsNoTracking()
                        .Where(o => o.FundId == fundId && o.Revision == treasury.Revision)
                        .ToListAsync(token);
                    var paid = (await db.V1FundContributions.AsNoTracking()
                        .Where(c => c.FundId == fundId)
                        .ToListAsync(token))
                        .GroupBy(c => c.MembershipId)
                        .ToDictionary(g => g.Key, g => g.Sum(c => c.AmountVnd));

                    if (obligations.Count == 0 || obligations.Any(o => paid.GetValueOrDefault(o.MembershipId) < o.RequiredVnd))
                        throw new ConflictException("obligations_not_completed", "Fixed fund spending requires all obligations to be satisfied.");

                    var lockedExcess = obligations.Sum(o => Math.Max(0, paid.GetValueOrDefault(o.MembershipId) - o.RequiredVnd));
                    var activeHolds = await db.V1FundRequests.AsNoTracking()
                        .Where(r => r.FundId == fundId && r.State == "Pending24h")
                        .SumAsync(r => r.AmountVnd, token);

                    var availableBalance = Math.Max(0, treasuryAccount.Balance - lockedExcess - activeHolds);
                    if (request.AmountVnd > availableBalance)
                        throw new ConflictException("insufficient_fund_balance", "Amount exceeds available balance after locked excess and holds.");

                    // Large expense policy: threshold 20%
                    var threshold = (long)Math.Floor(availableBalance * 0.20m);
                    var past24hStart = clock.UtcNow.AddHours(-24);
                    var recentSpent24h = await db.V1FundRequests.AsNoTracking()
                        .Where(r => r.FundId == fundId && r.Kind == "Expense" && (r.State == "Completed" || r.State == "Pending24h") && r.CreatedAt >= past24hStart)
                        .SumAsync(r => r.AmountVnd, token);

                    if (request.AmountVnd >= threshold || (recentSpent24h + request.AmountVnd >= threshold && threshold > 0))
                    {
                        // Enters 24h cooloff hold
                        var pendingItem = new V1FundRequest
                        {
                            FundId = fundId,
                            RequestedByUserId = actor,
                            RecipientUserId = request.RecipientUserId,
                            Kind = "Expense",
                            AmountVnd = request.AmountVnd,
                            Purpose = purpose,
                            IdempotencyKey = key,
                            OwnershipRevision = fund.OwnershipRevision,
                            State = "Pending24h"
                        };
                        db.V1FundRequests.Add(pendingItem);
                        await ActivityAsync(fundId, actor, "ExpensePendingHold24h", token, request.RecipientUserId,
                            detail: $"Khoản chi lớn {request.AmountVnd:N0} đ chờ 24 giờ để các thành viên xác nhận.",
                            referenceId: pendingItem.Id);
                        await db.SaveChangesAsync(token);
                        return MapRequest(pendingItem);
                    }
                }
                else // Free fund expense
                {
                    if (request.AmountVnd > treasuryAccount.Balance)
                        throw new ConflictException("insufficient_fund_balance", "Amount exceeds fund balance.");
                }

                // Direct spend execution (< 20% or Free fund authorized spend)
                var destWallet = await db.Wallets.FirstOrDefaultAsync(
                    w => w.OwnerUserId == request.RecipientUserId && w.Status == WalletStatus.Active, token);

                Guid? ledgerTxId = null;
                if (destWallet is not null)
                {
                    var destAccount = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destWallet.Id, token);
                    await FreshAccountAsync(destAccount.Id, token);
                    var posting = await ledger.PostAsync(
                        treasury.LedgerAccountId,
                        destAccount.Id,
                        request.AmountVnd,
                        "VND",
                        "V1FundExpense",
                        fundId,
                        "v1-fund-expense:" + fundId.ToString("N"),
                        key,
                        purpose,
                        token);
                    ledgerTxId = posting.LedgerTransactionId;

                    db.FinancialTransactions.Add(new FinancialTransaction
                    {
                        OwnerUserId = request.RecipientUserId,
                        WalletId = destWallet.Id,
                        LedgerTransactionId = posting.LedgerTransactionId,
                        RelatedBusinessId = fundId,
                        Type = FinancialTransactionType.Expense,
                        Status = FinancialTransactionStatus.Completed,
                        Amount = request.AmountVnd,
                        Currency = "VND",
                        Description = purpose,
                        OccurredAt = clock.UtcNow
                    });
                }

                var item = new V1FundRequest
                {
                    FundId = fundId,
                    RequestedByUserId = actor,
                    RecipientUserId = request.RecipientUserId,
                    Kind = "Expense",
                    AmountVnd = request.AmountVnd,
                    Purpose = purpose,
                    IdempotencyKey = key,
                    OwnershipRevision = fund.OwnershipRevision,
                    State = "Completed",
                    ReviewedByUserId = actor,
                    ReviewedAt = clock.UtcNow
                };
                db.V1FundRequests.Add(item);
                await ActivityAsync(fundId, actor, "ExpenseExecuted", token, request.RecipientUserId,
                    detail: $"Đã chi {request.AmountVnd:N0} đ: {purpose}",
                    referenceId: item.Id);
                await db.SaveChangesAsync(token);
                return MapRequest(item);
            }
            else // Withdrawal
            {
                var item = new V1FundRequest
                {
                    FundId = fundId,
                    RequestedByUserId = actor,
                    RecipientUserId = request.RecipientUserId,
                    Kind = request.Kind,
                    AmountVnd = request.AmountVnd,
                    Purpose = purpose,
                    IdempotencyKey = key,
                    OwnershipRevision = fund.OwnershipRevision,
                    State = "AwaitingOwnerReview"
                };
                db.V1FundRequests.Add(item);
                await ActivityAsync(fundId, actor, "FundRequestCreated", token, request.RecipientUserId, referenceId: item.Id);
                await db.SaveChangesAsync(token);
                return MapRequest(item);
            }
        }, ct);
    }

    public async Task<V1FundRequestDto> ReviewFundRequestAsync(Guid actor, Guid id, bool approved, string reason, string pin, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");
        return await ChangeFundRequestAsync(actor, id, pin, async (fund, item, token) =>
        {
            RequireOwner(fund, actor);
            if (item.State != "AwaitingOwnerReview" && item.State != "Pending24h")
            {
                if ((approved && item.State == "Completed") || (!approved && item.State == "Rejected"))
                    return MapRequest(item);
                throw new ConflictException("fund_request_unavailable", "Request is not awaiting review.");
            }

            if (item.OwnershipRevision != fund.OwnershipRevision)
                throw new ConflictException("fund_request_unavailable", "Fund ownership changed.");

            if (!approved)
            {
                item.State = "Rejected";
                item.ReviewedByUserId = actor;
                item.ReviewedAt = clock.UtcNow;
                item.ReviewReason = purpose;
                await ActivityAsync(fund.Id, actor, "FundRequestRejected", token, item.RequestedByUserId, detail: purpose, referenceId: id);
                return MapRequest(item);
            }

            // Approved: execute funds transfer
            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);
            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

            if (treasuryAccount.Balance < item.AmountVnd)
                throw new ConflictException("insufficient_fund_balance", "Fund balance is insufficient to execute request.");

            var destWallet = await db.Wallets.FirstOrDefaultAsync(
                w => w.OwnerUserId == item.RecipientUserId && w.Status == WalletStatus.Active, token)
                ?? throw new ConflictException("wallet_unavailable", "Recipient has no active wallet to receive funds.");

            var destAccount = await db.LedgerAccounts.SingleAsync(a => a.WalletId == destWallet.Id, token);
            await FreshAccountAsync(destAccount.Id, token);
            var posting = await ledger.PostAsync(
                treasury.LedgerAccountId,
                destAccount.Id,
                item.AmountVnd,
                "VND",
                item.Kind == "Withdrawal" ? "V1FundWithdrawal" : "V1FundExpense",
                fund.Id,
                $"v1-fund-payout:{fund.Id:N}:{item.Id:N}",
                $"review-{id:N}",
                purpose,
                token);

            db.FinancialTransactions.Add(new FinancialTransaction
            {
                OwnerUserId = item.RecipientUserId,
                WalletId = destWallet.Id,
                LedgerTransactionId = posting.LedgerTransactionId,
                RelatedBusinessId = fund.Id,
                Type = item.Kind == "Withdrawal" ? FinancialTransactionType.Withdrawal : FinancialTransactionType.Expense,
                Status = FinancialTransactionStatus.Completed,
                Amount = item.AmountVnd,
                Currency = "VND",
                Description = purpose,
                OccurredAt = clock.UtcNow
            });

            item.State = "Completed";
            item.ReviewedByUserId = actor;
            item.ReviewedAt = clock.UtcNow;
            item.ReviewReason = purpose;

            await ActivityAsync(fund.Id, actor,
                item.Kind == "Withdrawal" ? "FundWithdrawalExecuted" : "ExpenseExecuted",
                token,
                item.RequestedByUserId,
                detail: $"Đã duyệt và chuyển {item.AmountVnd:N0} đ: {purpose}",
                referenceId: id);

            return MapRequest(item);
        }, ct);
    }

    public Task<V1FundRequestDto> CancelFundRequestAsync(Guid actor, Guid id, string pin, CancellationToken ct) =>
        ChangeFundRequestAsync(actor, id, pin, async (fund, item, token) =>
        {
            if (actor != item.RequestedByUserId && actor != fund.OwnerUserId)
                throw new ForbiddenException("fund_request_owner_required", "Only the requester or fund owner can cancel.");
            if (item.State == "Cancelled") return MapRequest(item);
            if (item.State is not ("AwaitingOwnerReview" or "Pending24h"))
                throw new ConflictException("fund_request_unavailable", "Request cannot be cancelled in its current state.");
            item.State = "Cancelled";
            item.CancelledAt = clock.UtcNow;
            await ActivityAsync(fund.Id, actor, "FundRequestCancelled", token, referenceId: id);
            return MapRequest(item);
        }, ct);

    public async Task<IReadOnlyList<V1FundRequestDto>> FundRequestsAsync(Guid actor, Guid fundId, int page, int pageSize, CancellationToken ct)
    {
        ValidatePage(page, pageSize);
        await RequireActorAsync(actor, ct);
        await RequireMemberAsync(actor, fundId, ct);
        var rows = await db.V1FundRequests.AsNoTracking().Where(x => x.FundId == fundId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return rows.Select(MapRequest).ToList();
    }

    private async Task<V1FundRequestDto> ChangeFundRequestAsync(Guid actor, Guid id, string pin,
        Func<V1Fund, V1FundRequest, CancellationToken, Task<V1FundRequestDto>> change, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        await security.VerifyAsync(actor, pin, ct);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor], token);
            var fundId = await db.V1FundRequests.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.FundId).SingleOrDefaultAsync(token)
                ?? throw new NotFoundException("fund_request_not_found", "Request not found.");
            var fund = await LockFundAsync(fundId, token);
            await RequireMemberAsync(actor, fundId, token);
            var item = await db.V1FundRequests.SingleAsync(x => x.Id == id, token);
            if (db is DbContext context) await context.Entry(item).ReloadAsync(token);
            var result = await change(fund, item, token);
            await db.SaveChangesAsync(token);
            return result;
        }, ct);
    }

    private static V1FundRequestDto MapRequest(V1FundRequest x)
    {
        var avail = x.State switch
        {
            "Completed" => "Executed",
            "Pending24h" => "Hold24h",
            "Rejected" => "Rejected",
            "Cancelled" => "Cancelled",
            _ => "AwaitingReview"
        };
        return new(x.Id, x.FundId, x.RequestedByUserId, x.RecipientUserId,
            x.Kind, x.AmountVnd, x.Purpose, x.State, x.ReviewedByUserId, x.ReviewedAt, avail);
    }
}
