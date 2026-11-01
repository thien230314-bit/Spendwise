using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.V1Groups;

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

        await activeBankGuard.EnsureActiveBankAsync(actor, ct);
        await security.VerifyAsync(actor, request.SecurityPin, ct);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            await LockUsersAsync([actor, request.RecipientUserId], token);
            var fund = await LockFundAsync(fundId, token);
            await RequireMemberAsync(actor, fundId, token);
            var membershipId = await db.V1FundMemberships
                .Where(m => m.FundId == fundId && m.UserId == actor && m.LeftAt == null)
                .Select(m => m.Id)
                .SingleAsync(token);

            var replay = await db.V1FundRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.FundId == fundId && x.RequestedByUserId == actor && x.IdempotencyKey == key, token);

            if (replay is not null)
            {
                if (replay.Kind != request.Kind || replay.AmountVnd != request.AmountVnd || replay.RecipientUserId != request.RecipientUserId || replay.Purpose != purpose)
                    throw new ConflictException("idempotency_conflict", "Request key belongs to different intent.");

                return MapRequest(replay);
            }

            if (fund.ActivityClosedAt is not null) throw new ConflictException("activity_closed", "Activity is closed.");
            if (fund.DissolvedAt is not null) throw new ConflictException("fund_already_dissolved", "Fund is already dissolved.");
            var hasActiveElection = await db.V1Elections.AnyAsync(e => e.FundId == fundId && e.FinishedAt == null, token);
            var hasFailedElectionPendingSettlement = fund.Kind == V1FundKind.Fixed && await db.V1Elections.AnyAsync(e => e.FundId == fundId && e.Phase == V1ElectionPhase.Failed && e.OwnershipRevision == fund.OwnershipRevision, token);
            if (hasActiveElection || hasFailedElectionPendingSettlement)
                throw new ConflictException("election_finance_paused", "Financial operations are paused during or following a failed election pending settlement.");

            if (request.Kind == "Withdrawal" && (fund.Kind != V1FundKind.Free || request.RecipientUserId != actor))
                throw new ForbiddenException("withdrawal_not_allowed", "Free fund members may request only their own withdrawal.");

            var isOwner = actor == fund.OwnerUserId;

            if (fund.Kind == V1FundKind.Free)
            {
                await SweepExpiredApprovedRequestsAsync(fundId, token);
            }

            // Free Fund member limit: max 1 active Expense and max 1 active Withdrawal
            if (fund.Kind == V1FundKind.Free && !isOwner)
            {
                if (request.Kind == "Expense")
                {
                    var hasActiveExpense = await db.V1FundRequests.AnyAsync(r =>
                        r.FundId == fundId
                        && r.RequestedByUserId == actor
                        && r.Kind == "Expense"
                        && (r.State == "AwaitingOwnerReview"
                            || (r.State == "ApprovedUnused" && (r.ReviewedAt == null || r.ReviewedAt.Value.AddDays(7) > clock.UtcNow))
                            || r.State == "PaymentProcessing"),
                        token);

                    if (hasActiveExpense)
                    {
                        throw new ConflictException("fund_active_expense_limit_exceeded",
                            "Thành viên chỉ được có tối đa một yêu cầu chi tiêu đang hoạt động trong cùng quỹ.");
                    }
                }
                else if (request.Kind == "Withdrawal")
                {
                    var hasActiveWithdrawal = await db.V1FundRequests.AnyAsync(r =>
                        r.FundId == fundId
                        && r.RequestedByUserId == actor
                        && r.Kind == "Withdrawal"
                        && r.State == "AwaitingOwnerReview",
                        token);

                    if (hasActiveWithdrawal)
                    {
                        throw new ConflictException("fund_active_withdrawal_limit_exceeded",
                            "Thành viên chỉ được có tối đa một yêu cầu rút vốn đang hoạt động trong cùng quỹ.");
                    }
                }
            }

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

                // Verify ALL confirmed seats are occupied by active members (no vacant seats)
                var activeMembers = await ActiveMembersAsync(fundId, token);
                var confirmedSeats = await db.V1FundObligations.AsNoTracking()
                    .Where(o => o.FundId == fundId && o.Revision == treasuryPre.Revision)
                    .Select(o => o.Seat)
                    .Distinct()
                    .ToListAsync(token);

                if (activeMembers.Count != confirmedSeats.Count || !confirmedSeats.All(s => activeMembers.Any(m => m.Seat == s)))
                {
                    throw new ConflictException("fixed_vacant_seats", "Quỹ Cố định đang có ghế trống chưa được thay thế, không thể tạo yêu cầu chi tiêu.");
                }

                // Calculate total paid ONLY from CURRENT active members (historical refunded contributions of departed members CANNOT count towards target!)
                var activePaid = activeMembers.Sum(m => paidDict.GetValueOrDefault(m.Id, 0L));
                var targetVnd = (treasuryPre.TargetVnd ?? fund.TargetVnd) ?? 0L;
                if (targetVnd <= 0 || activePaid < targetVnd)
                {
                    throw new ConflictException("fixed_group_funding_incomplete", "Quỹ Cố định chưa hoàn thành 100% mục tiêu đóng góp của nhóm từ các thành viên hiện hành.");
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
                else // Free fund owner direct expense: only from unreserved balance
                {
                    var activeReservations = await db.V1FundRequests.AsNoTracking().Where(r =>
                        r.FundId == fundId
                        && r.Kind == "Expense"
                        && ((r.State == "ApprovedUnused" && (r.ReviewedAt == null || r.ReviewedAt.Value.AddDays(7) > clock.UtcNow))
                            || r.State == "PaymentProcessing"))
                        .SumAsync(r => r.AmountVnd, token);

                    var unreservedBalance = Math.Max(0, treasuryAccount.Balance - activeReservations);
                    if (request.AmountVnd > unreservedBalance)
                    {
                        throw new ConflictException("insufficient_unreserved_fund_balance",
                            "Khoản chi trực tiếp của chủ quỹ vượt quá số dư khả dụng chưa phân bổ của quỹ.");
                    }
                }

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

                if (fund.Kind == V1FundKind.Free)
                {
                    await AutoCancelExcessWithdrawalsAsync(fund, treasury, token);
                }

                return MapRequest(item);
            }
            else // Free fund requests requiring owner review
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
                if ((approved && (item.State == "Completed" || item.State == "ApprovedUnused")) || (!approved && item.State == "Rejected"))
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

            await RequireMemberAsync(item.RequestedByUserId, fund.Id, token);
            if ((item.RequestedByMembershipId is { } membershipId && !await db.V1FundMemberships.AnyAsync(m => m.Id == membershipId && m.LeftAt == null, token))
                || (item.RequestedByMembershipId == null && await db.V1FundMemberships.AnyAsync(m => m.FundId == fund.Id && m.UserId == item.RequestedByUserId && m.LeftAt != null, token)))
                throw new ConflictException("fund_request_membership_changed", "The requesting membership episode has ended.");

            await RequireActorAsync(item.RecipientUserId, token);
            await RequireNoActiveElectionAsync(fund.Id, token);

            var treasury = await TreasuryAsync(fund, token);
            await RequireReconciledAccountingAsync(treasury, token);
            var treasuryAccount = await FreshAccountAsync(treasury.LedgerAccountId, token);

            if (fund.Kind == V1FundKind.Free)
            {
                await SweepExpiredApprovedRequestsAsync(fund.Id, token);
            }

            // Free Fund Expense: ApprovedUnused (no funds transfer, valid for 7 days)
            if (fund.Kind == V1FundKind.Free && item.Kind == "Expense")
            {
                var otherReservations = await db.V1FundRequests.AsNoTracking().Where(r =>
                    r.FundId == fund.Id
                    && r.Id != item.Id
                    && r.Kind == "Expense"
                    && ((r.State == "ApprovedUnused" && (r.ReviewedAt == null || r.ReviewedAt.Value.AddDays(7) > clock.UtcNow))
                        || r.State == "PaymentProcessing"))
                    .SumAsync(r => r.AmountVnd, token);

                var unreserved = Math.Max(0, treasuryAccount.Balance - otherReservations);
                if (item.AmountVnd > unreserved)
                {
                    throw new ConflictException("insufficient_unreserved_fund_balance",
                        "Số dư khả dụng chưa phân bổ của quỹ không đủ để cấp quyền chi.");
                }

                item.State = "ApprovedUnused";
                item.ReviewedByUserId = actor;
                item.ReviewedAt = clock.UtcNow;
                item.ReviewReason = purpose;

                await ActivityAsync(fund.Id, actor, "FundExpenseApproved", token, item.RequestedByUserId,
                    detail: $"Đã duyệt quyền chi {item.AmountVnd:N0} đ (chưa sử dụng, hiệu lực 7 ngày): {purpose}",
                    referenceId: id);

                return MapRequest(item);
            }

            // Free Fund Withdrawal: Blocked if any active ApprovedUnused/PaymentProcessing exists; must not exceed member equity
            if (fund.Kind == V1FundKind.Free && item.Kind == "Withdrawal")
            {
                var hasActiveApprovedExpense = await db.V1FundRequests.AnyAsync(r =>
                    r.FundId == fund.Id
                    && r.Kind == "Expense"
                    && ((r.State == "ApprovedUnused" && (r.ReviewedAt == null || r.ReviewedAt.Value.AddDays(7) > clock.UtcNow))
                        || r.State == "PaymentProcessing"),
                    token);

                if (hasActiveApprovedExpense)
                {
                    throw new ConflictException("fund_withdrawal_blocked_by_active_expense",
                        "Không thể phê duyệt rút tiền cá nhân khi quỹ đang có quyền chi tiêu đã duyệt chưa hoàn tất hoặc chưa hủy.");
                }

                var equityResult = await CalculateFreeFundEquityAsync(fund, treasury, null, token);
                var memberEquity = equityResult.MemberEquities.SingleOrDefault(m => m.UserId == item.RequestedByUserId)?.EquityVnd ?? 0L;
                if (item.AmountVnd > memberEquity)
                {
                    throw new ConflictException("insufficient_member_equity",
                        "Số tiền rút vượt quá vốn cá nhân hiện tại của thành viên trong quỹ.");
                }
            }

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

            if (fund.Kind == V1FundKind.Free)
            {
                await AutoCancelExcessWithdrawalsAsync(fund, treasury, token);
            }

            return MapRequest(item);
        }, ct, approved);
    }

    public async Task<V1FundRequestDto> InitiateExpensePaymentAsync(Guid actor, Guid id, string pin, CancellationToken ct)
    {
        await RequireActorAsync(actor, ct);
        if (pin is not null)
            await security.VerifyAsync(actor, pin, ct);

        // 1. Authorization and boundary verification FIRST (outsiders and unrelated members rejected):
        var reqMeta = await db.V1FundRequests.AsNoTracking().Where(r => r.Id == id)
            .Select(r => new { r.FundId, r.RequestedByUserId, r.Kind, r.State, r.ReviewedAt, r.AmountVnd })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("fund_request_not_found", "Request not found.");

        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == reqMeta.FundId, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");

        await RequireMemberAsync(actor, fund.Id, ct);

        if (fund.Kind != V1FundKind.Free || reqMeta.Kind != "Expense")
            throw new ConflictException("invalid_fund_request", "Chỉ áp dụng thanh toán cho quyền chi tiêu Quỹ Tự do.");

        if (actor != reqMeta.RequestedByUserId && actor != fund.OwnerUserId)
            throw new ForbiddenException("fund_request_permission_required", "Chỉ người yêu cầu hoặc chủ quỹ mới có thể bắt đầu thanh toán.");

        // 2. Expiration check & atomic transition with system actor (null):
        if (reqMeta.State == "ApprovedUnused" && reqMeta.ReviewedAt is { } reviewedAt && clock.UtcNow >= reviewedAt.AddDays(7))
        {
            await ExpireSingleApprovedUnusedRequestAsync(id, ct);
            throw new ConflictException("fund_request_expired", "Quyền chi đã hết hạn sau 7 ngày.");
        }

        if (reqMeta.State == "Expired")
            throw new ConflictException("fund_request_expired", "Quyền chi đã hết hạn sau 7 ngày.");

        if (reqMeta.State != "ApprovedUnused")
            throw new ConflictException("fund_request_unavailable", "Chỉ quyền chi ở trạng thái ApprovedUnused mới có thể bắt đầu thanh toán.");

        // 3. External bill payment provider guard:
        // No payment provider is integrated in Stage 2. To avoid locking the fund in PaymentProcessing indefinitely,
        // reject safely with fund_payment_provider_unavailable while keeping ApprovedUnused.
        throw new ConflictException("fund_payment_provider_unavailable",
            "Cổng thanh toán hóa đơn đối ngoại chưa sẵn sàng. Quyền chi vẫn được giữ ở trạng thái ApprovedUnused.");
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

            if (item.State == "PaymentProcessing")
                throw new ConflictException("fund_payment_processing", "Không thể hủy quyền chi đang trong quá trình xử lý thanh toán bên ngoài.");

            if (item.State is not ("AwaitingOwnerReview" or "ApprovedUnused" or "Pending24h" or "Suspended"))
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
        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == fundId, ct)
            ?? throw new NotFoundException("fund_not_found", "Fund not found.");

        await RequireMemberAsync(actor, fundId, ct);

        if (fund.Kind == V1FundKind.Free)
        {
            await SweepExpiredApprovedRequestsAsync(fundId, ct);
        }

        var isOwner = actor == fund.OwnerUserId;
        DateTime? becameOwnerAt = null;
        if (isOwner && fund.OwnershipRevision > 0)
        {
            becameOwnerAt = await GetOwnerTenureStartedAtAsync(fund.Id, actor, fund.OwnershipRevision, ct);
        }

        var query = db.V1FundRequests.AsNoTracking()
            .Where(x => x.FundId == fundId);

        if (!isOwner)
        {
            query = query.Where(x => x.RequestedByUserId == actor);
        }
        else if (becameOwnerAt.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= becameOwnerAt.Value || x.RequestedByUserId == actor);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return rows.Select(MapRequest).ToList();
    }

    public async Task<int> ExpireStaleApprovedUnusedRequestsAsync(Guid? fundId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-7);

        var query = db.V1FundRequests.AsNoTracking()
            .Where(r => r.Kind == "Expense" && r.State == "ApprovedUnused" && r.ReviewedAt != null && r.ReviewedAt <= cutoff);

        if (fundId.HasValue)
        {
            query = query.Where(r => r.FundId == fundId.Value);
        }

        var candidateIds = await query.Select(r => r.Id).ToListAsync(ct);
        if (candidateIds.Count == 0) return 0;

        var expiredCount = 0;
        foreach (var id in candidateIds)
        {
            if (await ExpireSingleApprovedUnusedRequestAsync(id, ct))
            {
                expiredCount++;
            }
        }

        return expiredCount;
    }

    private Task<int> SweepExpiredApprovedRequestsAsync(Guid fundId, CancellationToken token) =>
        ExpireStaleApprovedUnusedRequestsAsync(fundId, token);

    /// <summary>
    /// Atomically transitions an expired ApprovedUnused expense request to Expired within a single ACID transaction.
    /// Locks the fund, re-verifies under exclusive lock directly from database, updates State = Expired,
    /// logs the FundRequestExpired activity (system actor), and saves changes atomically.
    /// </summary>
    private async Task<bool> ExpireSingleApprovedUnusedRequestAsync(Guid requestId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now.AddDays(-7);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            var reqMeta = await db.V1FundRequests.AsNoTracking()
                .Where(r => r.Id == requestId)
                .Select(r => new { r.FundId, r.Kind })
                .SingleOrDefaultAsync(token);

            if (reqMeta is null || reqMeta.Kind != "Expense") return false;

            // 1. Acquire exclusive fund lock first to eliminate race conditions between workers
            await LockFundAsync(reqMeta.FundId, token);

            // 2. Re-read fresh request state directly from database under exclusive lock
            var req = await db.V1FundRequests.SingleOrDefaultAsync(r => r.Id == requestId, token);
            if (req is null) return false;
            if (db is DbContext context) await context.Entry(req).ReloadAsync(token);

            if (req.State != "ApprovedUnused" || req.ReviewedAt == null || req.ReviewedAt.Value > cutoff)
            {
                return false;
            }

            req.State = "Expired";
            req.CancelledAt = now;
            req.ReviewReason = "Quyền chi tự hết hạn sau 7 ngày kể từ lúc duyệt.";

            await ActivityAsync(req.FundId, null, "FundRequestExpired", token, req.RequestedByUserId,
                detail: $"Quyền chi {req.AmountVnd:N0} đ tự hết hạn sau 7 ngày kể từ lúc duyệt.",
                referenceId: req.Id);

            await db.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    private async Task AutoCancelExcessWithdrawalsAsync(V1Fund fund, V1FundTreasury treasury, CancellationToken token)
    {
        var pendingWithdrawals = await db.V1FundRequests
            .Where(r => r.FundId == fund.Id && r.Kind == "Withdrawal" && r.State == "AwaitingOwnerReview")
            .ToListAsync(token);

        if (pendingWithdrawals.Count == 0) return;

        var equityResult = await CalculateFreeFundEquityAsync(fund, treasury, null, token);
        var equitiesByUser = equityResult.MemberEquities.ToDictionary(m => m.UserId, m => m.EquityVnd);

        foreach (var pw in pendingWithdrawals)
        {
            var equity = equitiesByUser.GetValueOrDefault(pw.RequestedByUserId, 0L);
            if (pw.AmountVnd > equity)
            {
                pw.State = "Cancelled";
                pw.CancelledAt = clock.UtcNow;
                pw.ReviewReason = "Tự động hủy do vốn cá nhân không còn đủ sau khi quỹ phát sinh chi tiêu chung.";
                await ActivityAsync(fund.Id, null, "FundRequestAutoCancelled", token,
                    subject: pw.RequestedByUserId,
                    detail: $"Yêu cầu rút {pw.AmountVnd:N0} đ tự động hủy do vốn còn lại ({equity:N0} đ) không đủ.",
                    referenceId: pw.Id);
            }
        }
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

    private V1FundRequestDto MapRequest(V1FundRequest x) => MapRequest(x, clock.UtcNow);

    private static V1FundRequestDto MapRequest(V1FundRequest x, DateTime utcNow)
    {
        var state = x.State;
        if (state == "ApprovedUnused" && x.ReviewedAt is { } reviewedAt && utcNow >= reviewedAt.AddDays(7))
        {
            state = "Expired";
        }

        var avail = state switch
        {
            "Completed" => "Executed",
            "ApprovedUnused" => "ApprovedUnused",
            "PaymentProcessing" => "PaymentProcessing",
            "Expired" => "Expired",
            "Pending24h" => "Hold24h",
            "Suspended" => "Suspended",
            "Rejected" => "Rejected",
            "Cancelled" => "Cancelled",
            _ => "AwaitingReview"
        };

        return new(x.Id, x.FundId, x.RequestedByUserId, x.RecipientUserId,
            x.Kind, x.AmountVnd, x.Purpose, state, x.ReviewedByUserId, x.ReviewedAt, avail);
    }
}
