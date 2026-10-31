using Microsoft.EntityFrameworkCore;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Exceptions;

using SpendWise.Domain.Admin;

using SpendWise.Domain.Finance;

using SpendWise.Domain.V1Groups;
using SpendWise.Application.Notifications;



namespace SpendWise.Application.Features.V1Groups;



public sealed record V1FundRequestInput(string Kind, long AmountVnd, Guid RecipientUserId, string Purpose, string IdempotencyKey, string SecurityPin);

public sealed record V1FundReportRequest(string Reason);

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
        var requestActorUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor, ct);
        if (requestActorUser?.Status == SpendWise.Domain.Identity.UserStatus.Restricted)
            throw new AppException("account_restricted", "Tài khoản đang bị hạn chế giao dịch tài chính.", 403);

        await activeBankGuard.EnsureActiveBankAsync(actor, ct); await security.VerifyAsync(actor, request.SecurityPin, ct);



        return await db.ExecuteInTransactionAsync(async token =>

        {

            await LockUsersAsync([actor, request.RecipientUserId], token);

            var fund = await LockFundAsync(fundId, token);

            await RequireMemberAsync(actor, fundId, token);
            var membershipId = await db.V1FundMemberships.Where(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null).Select(m => m.Id).SingleAsync(token);



            var replay = await db.V1FundRequests.AsNoTracking().SingleOrDefaultAsync(x => x.FundId == fundId && x.RequestedByUserId == actor && x.IdempotencyKey == key, token);

            if (replay is not null)

            {

                if (replay.Kind != request.Kind || replay.AmountVnd != request.AmountVnd || replay.RecipientUserId != request.RecipientUserId || replay.Purpose != purpose)

                    throw new ConflictException("idempotency_conflict", "Request key belongs to different intent.");

                return MapRequest(replay);

            }



            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Activity is closed.");

            if (fund.DissolvedAt is not null) throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");
            if (await db.V1Elections.AnyAsync(e => e.FundId == fundId && e.FinishedAt == null, token))
                throw new ConflictException("election_finance_paused", "Financial operations are paused during an active election.");



            if (request.Kind == "Withdrawal" && (fund.Kind != V1FundKind.Free || request.RecipientUserId != actor))

                throw new ForbiddenException("withdrawal_not_allowed", "Free fund members may request only their own withdrawal.");



            var isOwner = actor == fund.OwnerUserId;

            if (request.Kind == "Expense" && fund.Kind == V1FundKind.Fixed)
            {
                if (!isOwner)
                {
                    var hasPermission = await db.V1FundMemberships.AnyAsync(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null && m.CanSpend, token);
                    if (!hasPermission)
                        throw new ForbiddenException("fund_spending_permission_required", "Spending permission is required.");
                }

                var treasuryPre = await db.V1FundTreasuries.AsNoTracking().SingleOrDefaultAsync(t => t.FundId == fundId, token)
                    ?? throw new ConflictException("treasury_not_found", "Fund treasury not initialized.");

                var myObligation = await db.V1FundObligations.AsNoTracking().SingleOrDefaultAsync(
                    o => o.FundId == fundId && o.Revision == treasuryPre.Revision && o.MembershipId == membershipId, token);

                if (myObligation is null || myObligation.RequiredVnd <= 0)
                {
                    throw new ConflictException("obligations_not_completed", "Nghĩa vụ cá nhân chưa được xác nhận trong phiên hiện hành.");
                }

                var paidDict = await ActivityPaidAsync(fundId, token);
                var myPaid = paidDict.GetValueOrDefault(membershipId);
                if (myPaid < myObligation.RequiredVnd)
                {
                    throw new ConflictException("fixed_member_contribution_incomplete", "Cá nhân người yêu cầu chi chưa hoàn thành 100% nghĩa vụ đóng góp.");
                }

                var totalPaidContributions = paidDict.Values.Sum();
                var targetVnd = (treasuryPre.TargetVnd ?? fund.TargetVnd) ?? 0L;
                if (targetVnd <= 0 || totalPaidContributions < targetVnd)
                {
                    throw new ConflictException("fixed_group_funding_incomplete", "Quỹ Cố định chưa hoàn thành 100% mục tiêu đóng góp của nhóm.");
                }
            }

            await RequireNoActiveElectionAsync(fundId, token);

            var treasury = await TreasuryAsync(fund, token);

            await RequireReconciledAccountingAsync(treasury, token);

            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

            if (request.Kind == "Expense" && (fund.Kind == V1FundKind.Fixed || isOwner))
            {
                var destWallet = await PayoutWalletAsync(request.RecipientUserId, token);

                if (fund.Kind == V1FundKind.Fixed)
                {
                    var availableBalance = await FixedSpendableAsync(fund, treasury, treasuryAccount, null, token);
                    if (request.AmountVnd > availableBalance)
                        throw new ConflictException("insufficient_fund_balance", "Amount exceeds available balance after locked excess and holds.");
                }
                else // Free fund expense
                {
                    if (request.AmountVnd > treasuryAccount.Balance)
                        throw new ConflictException("insufficient_fund_balance", "Amount exceeds fund balance.");
                }

                // Direct spend execution (No 20% limit and no 24h hold for Fixed fund under PO V2 rules)
                var destAccount = await db.LedgerAccounts.SingleOrDefaultAsync(a => a.WalletId == destWallet.Id
                    && a.Type == LedgerAccountType.Wallet && a.Currency == "VND", token)
                    ?? throw new ConflictException("wallet_unavailable", "Recipient wallet ledger account is unavailable.");

                await FreshAccountAsync(destAccount.Id, token);

                var requestId = Guid.NewGuid();
                var posting = await ledger.PostAsync(
                    treasury.LedgerAccountId,
                    destAccount.Id,
                    request.AmountVnd,
                    "VND",
                    "V1FundExpense",
                    requestId,
                    $"v1-fund-expense:{fundId:N}:{actor:N}",
                    key,
                    purpose,
                    token);

                db.FinancialTransactions.Add(new FinancialTransaction
                {
                    OwnerUserId = request.RecipientUserId,
                    WalletId = destWallet.Id,
                    LedgerTransactionId = posting.LedgerTransactionId,
                    RelatedBusinessId = requestId,
                    Type = FinancialTransactionType.Expense,
                    Status = FinancialTransactionStatus.Completed,
                    Amount = request.AmountVnd,
                    Currency = "VND",
                    Description = purpose,
                    OccurredAt = clock.UtcNow
                });

                var item = new V1FundRequest
                {
                    Id = requestId,
                    FundId = fundId,
                    RequestedByMembershipId = membershipId,
                    RequestedByUserId = actor,
                    RecipientUserId = request.RecipientUserId,
                    Kind = "Expense",
                    AmountVnd = request.AmountVnd,
                    Purpose = purpose,
                    IdempotencyKey = key,
                    OwnershipRevision = fund.OwnershipRevision,
                    CreatedAt = clock.UtcNow,
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

            else // Free fund requests require owner review

            {

                var item = new V1FundRequest

                {

                    FundId = fundId,
                    RequestedByMembershipId = membershipId,

                    RequestedByUserId = actor,

                    RecipientUserId = request.RecipientUserId,

                    Kind = request.Kind,

                    AmountVnd = request.AmountVnd,

                    Purpose = purpose,

                    IdempotencyKey = key,

                    OwnershipRevision = fund.OwnershipRevision,

                    CreatedAt = clock.UtcNow,

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

            if (fund.Kind == V1FundKind.Fixed)
            {
                throw new ConflictException("fixed_owner_review_disallowed", "Fixed fund expenses cannot be reviewed by owner.");
            }

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



            if (fund.ActivityClosedAt is not null)

                throw new ConflictException("activity_closed", "Activity is closed.");

            if (item.State == "Pending24h" && clock.UtcNow < item.CreatedAt.AddHours(24))

                throw new ConflictException("fund_request_hold_active", "The server-side 24-hour hold has not elapsed.");

            await RequireMemberAsync(item.RequestedByUserId, fund.Id, token);
            if ((item.RequestedByMembershipId is { } membershipId && !await db.V1FundMemberships.AnyAsync(m => m.Id == membershipId && m.LeftAt == null, token))
                || (item.RequestedByMembershipId == null && await db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == item.RequestedByUserId && m.LeftAt != null, token)))
                throw new ConflictException("fund_request_membership_changed", "The requesting membership episode has ended.");

            await RequireActorAsync(item.RecipientUserId, token);

            await RequireNoActiveElectionAsync(fund.Id, token);

            if (item.Kind == "Expense" && fund.Kind == V1FundKind.Fixed && item.RequestedByUserId != fund.OwnerUserId

                && !await db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == item.RequestedByUserId

                    && m.LeftAt == null && m.CanSpend, token))

                throw new ForbiddenException("fund_spending_permission_required", "Spending permission is required.");



            // Approved: execute funds transfer

            var treasury = await TreasuryAsync(fund, token);

            await RequireReconciledAccountingAsync(treasury, token);

            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);



            var available = fund.Kind == V1FundKind.Fixed

                ? await FixedSpendableAsync(fund, treasury, treasuryAccount, item.Id, token)

                : treasuryAccount.Balance;

            if (available < item.AmountVnd)

                throw new ConflictException("insufficient_fund_balance", "Fund balance is insufficient to execute request.");



            var destWallet = await PayoutWalletAsync(item.RecipientUserId, token);



            var destAccount = await db.LedgerAccounts.SingleOrDefaultAsync(a => a.WalletId == destWallet.Id

                    && a.Type == LedgerAccountType.Wallet && a.Currency == "VND", token)

                    ?? throw new ConflictException("wallet_unavailable", "Recipient wallet ledger account is unavailable.");

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

                RelatedBusinessId = item.Id,

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

        }, ct, approved);

    }



    public Task<V1FundRequestDto> ReportFundRequestAsync(Guid actor, Guid id, string reason, CancellationToken ct) =>

        ChangeFundRequestAsync(actor, id, null, async (fund, item, token) =>

        {
            var reportReason = InputBoundary.RequiredText(reason, 1000, "Report reason");

            // For Completed Fixed Fund expenses under V2, reporting opens a dispute SupportCase without altering financial state or reversing money
            if (item.State == "Completed")
            {
                if (item.Kind != "Expense")
                    throw new ConflictException("fund_request_not_reportable", "Chỉ các khoản chi tiêu (Expense) đã hoàn thành mới có thể được báo cáo.");

                // Idempotency: check if this actor has already opened a dispute for this request
                var existingCase = await db.SupportCases.AsNoTracking().FirstOrDefaultAsync(
                    c => c.Category == "V1FundDispute" && c.LinkedTransactionId == item.Id && c.LinkedUserId == actor, token);

                if (existingCase is not null)
                {
                    return MapRequest(item);
                }

                // Exact 1-to-1 correlation via exact request ID (never match ambiguous transactions by amount):
                var finTx = await db.FinancialTransactions.AsNoTracking().FirstOrDefaultAsync(
                    f => f.RelatedBusinessId == item.Id && f.Type == FinancialTransactionType.Expense, token);

                if (finTx is null)
                {
                    var ledgerTx = await db.LedgerTransactions.AsNoTracking().FirstOrDefaultAsync(
                        lt => lt.BusinessId == item.Id && (lt.BusinessType == "V1FundExpense" || lt.BusinessType == "V1FundRequest"), token);
                    if (ledgerTx is not null)
                    {
                        finTx = await db.FinancialTransactions.AsNoTracking().FirstOrDefaultAsync(
                            f => f.LedgerTransactionId == ledgerTx.Id, token);
                    }
                }

                var ledgerTxRef = finTx != null ? finTx.LedgerTransactionId.ToString() : "legacy_transaction_correlation_unresolved";

                var completedTicket = new SupportCase
                {
                    LinkedUserId = actor,
                    LinkedTransactionId = item.Id,
                    Category = "V1FundDispute",
                    Subject = $"Completed expense reported in fund {fund.Code}: {item.AmountVnd:N0} đ",
                    Description = $"Reported completed expense by user {actor} for fund {fund.Id}, request {item.Id}. Amount: {item.AmountVnd:N0} VND. LedgerTx: {ledgerTxRef}. Reason: {reportReason}",
                    Priority = "High",
                    Status = CaseStatus.New
                };
                completedTicket.TicketNumber = "V1-" + completedTicket.Id.ToString("N");
                db.SupportCases.Add(completedTicket);

                var reporterNameCompleted = await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);
                db.CaseHistories.Add(new CaseHistory
                {
                    CaseKind = CaseKind.Support,
                    CaseId = completedTicket.Id,
                    ActorId = actor,
                    ActorDisplayNameSnapshot = reporterNameCompleted,
                    ActorRole = "User",
                    Action = "Created",
                    Message = $"Completed expense reported: {reportReason}",
                    NewStatus = completedTicket.Status.ToString()
                });

                await ActivityAsync(fund.Id, actor, "FundExpenseReported", token, item.RequestedByUserId,
                    detail: $"Khoản chi đã hoàn thành {item.AmountVnd:N0} đ bị khiếu nại/báo cáo: {reportReason}",
                    referenceId: id);

                return MapRequest(item);
            }

            // Legacy / hold-based dispute
            if (item.State == "Suspended") return MapRequest(item);

            if (item.State != "Pending24h")
                throw new ConflictException("fund_request_not_reportable", "Only pending 24-hour holds or completed expenses can be reported.");

            item.State = "Suspended";
            item.ReviewReason = reportReason;

            // Open SupportCase for the dispute
            var ticket = new SupportCase
            {
                LinkedUserId = actor,
                LinkedTransactionId = item.Id,
                Category = "V1FundDispute",
                Subject = $"Expense reported in fund {fund.Code}: {item.AmountVnd:N0} đ",
                Description = $"Reported by user {actor} for fund {fund.Id}, request {item.Id}. Amount: {item.AmountVnd:N0} VND. Reason: {reportReason}",
                Priority = "High",
                Status = CaseStatus.New
            };
            ticket.TicketNumber = "V1-" + ticket.Id.ToString("N");
            db.SupportCases.Add(ticket);

            var reporterName = await db.Users.Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);
            db.CaseHistories.Add(new CaseHistory
            {
                CaseKind = CaseKind.Support,
                CaseId = ticket.Id,
                ActorId = actor,
                ActorDisplayNameSnapshot = reporterName,
                ActorRole = "User",
                Action = "Created",
                Message = $"Expense hold reported and suspended: {reportReason}",
                NewStatus = ticket.Status.ToString()
            });

            await ActivityAsync(fund.Id, actor, "FundExpenseSuspended", token, item.RequestedByUserId,
                detail: $"Khoản chi {item.AmountVnd:N0} đ bị báo cáo và tạm dừng: {reportReason}",
                referenceId: id);

            return MapRequest(item);

        }, ct);



    public Task<V1FundRequestDto> CancelFundRequestAsync(Guid actor, Guid id, string pin, CancellationToken ct) =>

        ChangeFundRequestAsync(actor, id, pin, async (fund, item, token) =>

        {

            if (actor != item.RequestedByUserId && actor != fund.OwnerUserId)

                throw new ForbiddenException("fund_request_owner_required", "Only the requester or fund owner can cancel.");

            if (item.State == "Cancelled") return MapRequest(item);

            if (item.State is not ("AwaitingOwnerReview" or "Pending24h" or "Suspended"))

                throw new ConflictException("fund_request_unavailable", "Request cannot be cancelled in its current state.");

            item.State = "Cancelled";

            item.CancelledAt = clock.UtcNow;

            await ActivityAsync(fund.Id, actor, "FundRequestCancelled", token, referenceId: id);

            return MapRequest(item);

        }, ct);



    /// <summary>
    /// Closes requests that cannot be approved after a change of owner or membership.
    /// Called only while the fund row is locked and inside the caller's transaction.
    /// Completed/Rejected/Suspended requests and ledger postings must never change here.
    /// </summary>
    private async Task CancelPendingFreeFundRequestsAsync(
        V1Fund fund, string reason, CancellationToken ct, Guid? requesterUserId = null)
    {
        if (fund.Kind != V1FundKind.Free) return;

        var pendingQuery = db.V1FundRequests.Where(r =>
            r.FundId == fund.Id && r.State == "AwaitingOwnerReview");
        if (requesterUserId is { } requester)
            pendingQuery = pendingQuery.Where(r => r.RequestedByUserId == requester);

        var pending = await pendingQuery.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToListAsync(ct);
        if (pending.Count == 0) return;

        var message = reason switch
        {
            "OwnershipChanged" => "Chủ nhóm đã thay đổi. Yêu cầu chi/rút đang chờ đã bị hủy; vui lòng tạo lại để chủ mới xét duyệt.",
            "MemberLeft" => "Yêu cầu chi/rút đang chờ đã bị hủy vì bạn đã rời quỹ.",
            "MemberKicked" => "Yêu cầu chi/rút đang chờ đã bị hủy vì bạn không còn là thành viên quỹ.",
            "FundDissolved" => "Yêu cầu chi/rút đang chờ đã bị hủy vì quỹ đã giải tán.",
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };

        foreach (var request in pending)
        {
            request.State = "Cancelled";
            request.CancelledAt = clock.UtcNow;
            db.V1FundActivities.Add(new V1FundActivity
            {
                FundId = fund.Id,
                ActorUserId = null,
                Event = "FundRequestAutoCancelled",
                SubjectUserId = request.RequestedByUserId,
                Detail = reason,
                ReferenceId = request.Id
            });
        }

        await db.SaveChangesAsync(ct);
        await notifications.CreateManyAsync(pending.Select(request => new CreateNotificationRequest(
            request.RequestedByUserId,
            "V1FundActivity",
            "Yêu cầu chi/rút đã hủy",
            message,
            new
            {
                FundId = fund.Id,
                RequestId = request.Id,
                Kind = request.Kind,
                State = "Cancelled",
                Reason = reason,
                ResubmitRequired = reason == "OwnershipChanged"
            })).ToList(), ct);
    }

    public async Task<IReadOnlyList<V1FundRequestDto>> FundRequestsAsync(Guid actor, Guid fundId, int page, int pageSize, CancellationToken ct)

    {

        ValidatePage(page, pageSize);

        await RequireActorAsync(actor, ct);

        await RequireMemberAsync(actor, fundId, ct);

        var rows = await db.V1FundRequests.AsNoTracking().Where(x => x.FundId == fundId).OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)

            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return rows.Select(MapRequest).ToList();

    }



    private async Task<V1FundRequestDto> ChangeFundRequestAsync(Guid actor, Guid id, string? pin,

        Func<V1Fund, V1FundRequest, CancellationToken, Task<V1FundRequestDto>> change, CancellationToken ct, bool payout = false)

    {

        await RequireActorAsync(actor, ct);

        if (pin is not null)

            await security.VerifyAsync(actor, pin, ct);

        return await db.ExecuteInTransactionAsync(async token =>

        {

            var identities = payout ? await db.V1FundRequests.AsNoTracking().Where(x => x.Id == id)

                .Select(x => new { x.RequestedByUserId, x.RecipientUserId }).SingleOrDefaultAsync(token) : null;

            await LockUsersAsync(identities is null ? [actor] : [actor, identities.RequestedByUserId, identities.RecipientUserId], token);

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



    private async Task<Wallet> PayoutWalletAsync(Guid recipient, CancellationToken ct)

    {

        await RequireActorAsync(recipient, ct);

        return await db.Wallets.Where(w => w.OwnerUserId == recipient && w.Status == WalletStatus.Active && w.Currency == "VND")

            .OrderByDescending(w => w.IsDefault).ThenBy(w => w.Id).FirstOrDefaultAsync(ct)

            ?? throw new ConflictException("wallet_unavailable", "Recipient has no active VND wallet to receive funds.");

    }



    private async Task<decimal> FixedSpendableAsync(V1Fund fund, V1FundTreasury treasury,

        LedgerAccount account, Guid? executingRequest, CancellationToken ct)

    {

        var state = await FinanceStateAsync(fund, treasury, ct);

        if (state.SpendingState != "OPEN")

            throw new ConflictException("obligations_not_completed", "Fixed fund spending requires current, satisfied obligations.");

        var holds = await db.V1FundRequests.AsNoTracking().Where(r => r.FundId == fund.Id

            && (r.State == "Pending24h" || r.State == "Suspended") && r.Id != executingRequest)

            .SumAsync(r => r.AmountVnd, ct);

        return Math.Max(0, account.Balance - state.Obligations.Sum(o => o.LockedExcessVnd) - holds);

    }



    private async Task<decimal> FixedWindowOpeningBalanceAsync(Guid fundId, V1FundTreasury treasury,

        LedgerAccount account, CancellationToken ct)

    {

        var start = clock.UtcNow.AddHours(-24);

        var firstExpense = await db.V1FundRequests.AsNoTracking().Where(r => r.FundId == fundId && r.Kind == "Expense"

            && (r.State == "Completed" || r.State == "Pending24h") && r.CreatedAt >= start)

            .OrderBy(r => r.CreatedAt).Select(r => (DateTime?)r.CreatedAt).FirstOrDefaultAsync(ct) ?? clock.UtcNow;

        var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.LedgerAccountId == account.Id

            && db.LedgerTransactions.Any(t => t.Id == e.LedgerTransactionId && t.CompletedAt >= start

                && !(t.BusinessType == "V1FundContribution" && t.CompletedAt <= firstExpense)))

            .Select(e => new { e.Direction, e.Amount }).ToListAsync(ct);

        var opening = account.Balance - entries.Sum(e => e.Direction == LedgerEntryDirection.Debit ? e.Amount : -e.Amount);

        // Initial funding before the first expense establishes the opening balance of a new spending window.

        var state = await FinanceStateAsync(await FundAsync(fundId, ct), treasury, ct);

        var priorHolds = await db.V1FundRequests.AsNoTracking().Where(r => r.FundId == fundId

            && (r.State == "Pending24h" || r.State == "Suspended") && r.CreatedAt < start).SumAsync(r => r.AmountVnd, ct);

        return Math.Max(0, opening - state.Obligations.Sum(o => o.LockedExcessVnd) - priorHolds);

    }



    private static V1FundRequestDto MapRequest(V1FundRequest x)

    {

        var avail = x.State switch

        {

            "Completed" => "Executed",

            "Pending24h" => "Hold24h",

            "Suspended" => "Suspended",

            "Rejected" => "Rejected",

            "Cancelled" => "Cancelled",

            _ => "AwaitingReview"

        };

        return new(x.Id, x.FundId, x.RequestedByUserId, x.RecipientUserId,

            x.Kind, x.AmountVnd, x.Purpose, x.State, x.ReviewedByUserId, x.ReviewedAt, avail);

    }

}

