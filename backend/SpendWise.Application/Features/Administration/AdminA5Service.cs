using System.Security.Claims;

using Microsoft.EntityFrameworkCore;

using SpendWise.Application.Admin;

using SpendWise.Application.Common;

using SpendWise.Application.Common.Exceptions;

using SpendWise.Application.Common.Interfaces;

using SpendWise.Application.Features.Admin;

using SpendWise.Application.Features.Admin.Cases;

using SpendWise.Application.Identity;

using SpendWise.Application.Notifications;

using SpendWise.Domain.Admin;

using SpendWise.Domain.Finance;

using SpendWise.Domain.Groups;

using SpendWise.Domain.Identity;

using SpendWise.Domain.V1Groups;



namespace SpendWise.Application.Features.Administration;



public sealed class AdminA5Service(

    ISpendWiseDbContext db,

    PlatformPermissionService permissions,

    V1CaseWorkflow workflow,

    AdminAuditWriter audit,

    INotificationService notifications,

    SupportCaseEmailSupport supportEmails,

    IEmailTemplateRenderer templateRenderer,

    CaseAttachmentValidator attachmentValidator,

    IClock clock)

{

    // A5.1 - Ticket mới & tra cứu

    public async Task<A2Page<A5TicketListItem>> TicketsAsync(ClaimsPrincipal principal, A5TicketQuery query, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(query.Reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);

        ValidatePage(query.Page, query.PageSize);

        ValidateDateRange(query.FromUtc, query.ToUtc);



        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))

            throw new ForbiddenException("operations_read_denied", "An active operator session is required.");



        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var rows = db.SupportCases.AsNoTracking().Where(c => c.AssignedAdminId == actor || c.AssignedAdminId == null);



        if (!string.IsNullOrWhiteSpace(query.Status))

        {

            var parsedStatus = CaseWorkflowMappings.ParseSupport(query.Status);

            rows = rows.Where(c => c.Status == parsedStatus);

        }



        if (!string.IsNullOrWhiteSpace(query.Category))

        {

            var cleanCat = query.Category.Trim();

            rows = rows.Where(c => c.Category == cleanCat);

        }



        if (!string.IsNullOrWhiteSpace(query.Priority))

        {

            var cleanPriority = query.Priority.Trim();

            rows = rows.Where(c => c.Priority == cleanPriority);

        }



        if (query.LinkedUserId is { } uid)

        {

            rows = rows.Where(c => c.LinkedUserId == uid);

        }



        if (query.LinkedTransactionId is { } txId)

        {

            rows = rows.Where(c => c.LinkedTransactionId == txId);

        }



        if (!string.IsNullOrWhiteSpace(query.SearchText))

        {

            var text = query.SearchText.Trim();

            rows = rows.Where(c => c.TicketNumber.Contains(text) || c.Subject.Contains(text) || c.Description.Contains(text));

        }



        if (query.FromUtc is { } from) rows = rows.Where(c => c.CreatedAt >= from);

        if (query.ToUtc is { } to) rows = rows.Where(c => c.CreatedAt < to);



        var total = await rows.CountAsync(ct);



        var items = await (from c in rows

                           join u in db.Users.AsNoTracking() on c.LinkedUserId equals u.Id into uGroup

                           from user in uGroup.DefaultIfEmpty()

                           orderby c.CreatedAt descending, c.Id

                           select new A5TicketListItem(

                               c.Id,

                               c.TicketNumber,

                               c.LinkedUserId,

                               user != null ? user.SpendId : null,

                               user != null ? user.FullName : null,

                               c.Category,

                               c.Priority,

                               c.Status.ToString(),

                               c.Subject,

                               c.Description,

                               c.LinkedTransactionId,

                               c.AssignedAdminId,

                               DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),

                               c.UpdatedAt != null ? DateTime.SpecifyKind(c.UpdatedAt.Value, DateTimeKind.Utc) : null,

                               c.LatestReplyAt != null ? DateTime.SpecifyKind(c.LatestReplyAt.Value, DateTimeKind.Utc) : null

                           ))

                           .Skip((query.Page - 1) * query.PageSize)

                           .Take(query.PageSize)

                           .ToListAsync(ct);



        audit.Add(actor, "A5_TICKETS_SEARCH", "SupportCase", null, purpose,

            new { Count = items.Count, Page = query.Page, Total = total });

        await db.SaveChangesAsync(ct);



        return new A2Page<A5TicketListItem>(clock.UtcNow, query.Page, query.PageSize, total, items);

    }



    // A5.2 - Đang xử lý / Phân công (Assign or Claim)

    public async Task<bool> AssignTicketAsync(ClaimsPrincipal principal, Guid caseId, A5AssignTicketRequest request, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(request.Reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);



        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))

            throw new ForbiddenException("permission_denied", "Operator required.");



        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);




        // Admin assigns case to self (or target operator)

        var targetOperatorId = request.OperatorId == Guid.Empty ? actor : request.OperatorId;

        if (targetOperatorId != actor)

            throw new ForbiddenException("assignment_denied", "Ordinary admin can only assign cases to self.");



        return await db.ExecuteInTransactionAsync(async token =>

        {

            var ticket = await db.SupportCases.SingleOrDefaultAsync(c => c.Id == caseId, token)

                ?? throw new NotFoundException("case_not_found", "Case not found.");



            if (!await db.AcquireGroupAdmissionLockAsync(ticket.LinkedUserId, token))

                throw new NotFoundException("user_not_found", "User not found.");



            if (ticket.Status is CaseStatus.Closed or CaseStatus.Rejected or CaseStatus.Resolved)

                throw new ConflictException("case_closed", "Closed cases cannot be assigned.");



            // FIX-2: Prevent ordinary admin from stealing another admin's ticket

            if (ticket.AssignedAdminId != null && ticket.AssignedAdminId != actor)

                throw new ForbiddenException("assignment_denied", "Case is already assigned to another Admin.");



            // Verify target operator is active Admin

            var operatorUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == targetOperatorId, token);

            if (operatorUser == null || operatorUser.SystemRole != SystemRole.Admin || operatorUser.Status != UserStatus.Active)

                throw new ConflictException("operator_not_ready", "Target operator must be an active ordinary Admin.");



            if (ticket.AssignedAdminId == targetOperatorId && ticket.Status == CaseStatus.Investigating)

                return true;



            var oldAdminId = ticket.AssignedAdminId;

            ticket.AssignedAdminId = targetOperatorId;

            if (ticket.Status == CaseStatus.New || ticket.Status == CaseStatus.Open)

            {

                ticket.Status = CaseStatus.Investigating;

            }

            ticket.UpdatedAt = clock.UtcNow;



            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);

            db.CaseHistories.Add(new CaseHistory

            {

                CaseId = caseId,

                CaseKind = CaseKind.Support,

                ActorId = actor,

                ActorDisplayNameSnapshot = actorName,

                ActorRole = "Admin",

                Action = "A5_ASSIGNED",

                Message = purpose,

                NewStatus = ticket.Status.ToString()

            });



            audit.Add(actor, "A5_CASE_ASSIGNED", "SupportCase", caseId.ToString(), purpose,

                new { PreviousAdmin = oldAdminId, AssignedAdmin = targetOperatorId, Status = ticket.Status.ToString() });

            await db.SaveChangesAsync(token);



            await notifications.CreateAsync(targetOperatorId, "AdminTaskAssigned", "Case assigned",

                $"Support case {ticket.TicketNumber} has been assigned to you.", new { CaseId = caseId }, token);



            return true;

        }, ct);

    }



    // A5.2 - Lấy danh sách ticket đang xử lý được phân công

    public async Task<IReadOnlyList<A5AssignedCaseItem>> AssignedCasesAsync(ClaimsPrincipal principal, string reason, int page, int pageSize, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);

        ValidatePage(page, pageSize);



        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))

            throw new ForbiddenException("permission_denied", "Operator required.");



        var actor = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var rows = await db.SupportCases.AsNoTracking()

            .Where(c => (c.AssignedAdminId == actor || c.AssignedAdminId == null)

                && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Rejected && c.Status != CaseStatus.Resolved)

            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)

            .ThenBy(c => c.Id)

            .Skip((page - 1) * pageSize)

            .Take(pageSize)

            .Select(c => new A5AssignedCaseItem(

                c.Id,

                c.TicketNumber,

                c.Status.ToString(),

                c.Priority,

                c.Category,

                c.Subject,

                c.LinkedUserId,

                c.AssignedAdminId,

                DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc),

                c.LatestReplyAt != null ? DateTime.SpecifyKind(c.LatestReplyAt.Value, DateTimeKind.Utc) : null

            ))

            .ToListAsync(ct);



        audit.Add(actor, "A5_ASSIGNED_CASES_READ", "SupportQueue", null, purpose, new { Count = rows.Count, Page = page });

        await db.SaveChangesAsync(ct);

        return rows;

    }



    // A5.3 - Chờ User phản hồi (Request info from user)

    public Task<bool> RequestInfoAsync(ClaimsPrincipal principal, Guid caseId, A5RequestInfoRequest request, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(request.Reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);

        var message = InputBoundary.RequiredText(request.Message, 2000, "Message");



        return workflow.WithCaseAsync(principal, caseId, purpose, false, async (ticket, actor, token) =>

        {

            if (ticket.Status is CaseStatus.Closed or CaseStatus.Rejected or CaseStatus.Resolved)

                throw new ConflictException("case_closed", "Closed cases cannot transition.");



            var oldStatus = ticket.Status.ToString();

            ticket.Status = CaseStatus.WaitingForUser;

            ticket.LatestReplyAt = clock.UtcNow;

            ticket.UpdatedAt = clock.UtcNow;



            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);

            var history = new CaseHistory

            {

                CaseId = caseId,

                CaseKind = CaseKind.Support,

                ActorId = actor,

                ActorDisplayNameSnapshot = actorName,

                ActorRole = "Admin",

                Action = "request_info",

                Message = message,

                OldStatus = oldStatus,

                NewStatus = CaseStatus.WaitingForUser.ToString()

            };

            db.CaseHistories.Add(history);



            if (request.Attachments is { Count: > 0 } uploads)

            {

                foreach (var att in attachmentValidator.Create(ticket.LinkedUserId, history.Id, uploads))

                {

                    db.CaseAttachments.Add(att);

                }

            }



            audit.Add(actor, "A5_REQUEST_INFO", "SupportCase", caseId.ToString(), purpose,

                new { OldStatus = oldStatus, NewStatus = CaseStatus.WaitingForUser.ToString() });

            await db.SaveChangesAsync(token);



            await notifications.CreateAsync(ticket.LinkedUserId, "SupportUpdated", "Thông tin bổ sung được yêu cầu",

                message, new { supportCaseId = caseId, status = "need_more_info", actionRoute = $"/support/{caseId}" }, token);



            return true;

        }, ct);

    }



    // A5.4 - Hồ sơ điều tra quỹ (Dossier)

    public Task<A5FundDossierResult> FundDossierAsync(ClaimsPrincipal principal, Guid caseId, string reason, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);



        return workflow.WithCaseAsync(principal, caseId, purpose, false, async (ticket, actor, token) =>

        {

            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ticket.LinkedUserId, token);



            // Read case histories for timeline

            var histories = await db.CaseHistories.AsNoTracking()

                .Include(h => h.Attachments)

                .Where(h => h.CaseId == caseId && h.CaseKind == CaseKind.Support)

                .OrderBy(h => h.CreatedAt)

                .ToListAsync(token);



            var timeline = histories.Select(h => new A5DossierTimelineEvent(

                DateTime.SpecifyKind(h.CreatedAt, DateTimeKind.Utc),

                h.ActorRole,

                h.ActorDisplayNameSnapshot,

                h.Action,

                h.Message

            )).ToList();



            var attachments = histories.SelectMany(h => h.Attachments)

                .Select(a => new A5DossierAttachmentItem(

                    a.Id,

                    a.OriginalFileName,

                    a.ContentType,

                    a.Length,

                    DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc)

                )).ToList();



            // Read linked disputes: FIX-3 - Only include disputes structurally linked to the case's linked transaction

            var disputes = new List<A5DossierDisputeItem>();

            if (ticket.LinkedTransactionId is { } caseTxId)

            {

                disputes = await db.Disputes.AsNoTracking()

                    .Where(d => d.UserId == ticket.LinkedUserId && d.LinkedTransactionId == caseTxId)

                    .OrderByDescending(d => d.CreatedAt)

                    .Select(d => new A5DossierDisputeItem(

                        d.Id,

                        d.LinkedTransactionId,

                        d.Status.ToString(),

                        d.Reason,

                        d.TransactionAmountSnapshot,

                        DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc)

                    ))

                    .ToListAsync(token);

            }



            // Check linked fund/group if category or linked transaction relates to fund

            A5LinkedFundOverview? linkedFund = null;

            var balanceVerified = false;



            // 1. Try finding via linked transaction or user group membership

            if (ticket.LinkedTransactionId is { } txId)

            {

                var tx = await db.FinancialTransactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == txId, token);

                if (tx?.RelatedBusinessId is { } busId)

                {

                    // Check V1 Fund

                    var v1Contrib = await db.V1FundContributions.AsNoTracking().SingleOrDefaultAsync(c => c.Id == busId, token);

                    if (v1Contrib != null)

                    {

                        var fund = await db.V1Funds.AsNoTracking().SingleOrDefaultAsync(f => f.Id == v1Contrib.FundId, token);

                        if (fund != null)

                        {

                            var memberCount = await db.V1FundMemberships.CountAsync(m => m.FundId == fund.Id && m.LeftAt == null, token);

                            var totalContrib = await db.V1FundContributions.Where(c => c.FundId == fund.Id).SumAsync(c => c.AmountVnd, token);

                            var membership = await db.V1FundMemberships.FirstOrDefaultAsync(m => m.FundId == fund.Id && m.UserId == ticket.LinkedUserId, token);

                            var treasury = await db.V1FundTreasuries.AsNoTracking().FirstOrDefaultAsync(t => t.FundId == fund.Id, token);

                            decimal fundBalance = 0;

                            if (treasury != null)

                            {

                                var acc = await db.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == treasury.LedgerAccountId, token);

                                if (acc != null)

                                {

                                    var debits = await db.LedgerEntries.Where(e => e.LedgerAccountId == acc.Id && e.Direction == LedgerEntryDirection.Debit && e.LedgerTransaction.Status == LedgerTransactionStatus.Completed).SumAsync(e => e.Amount, token);

                                    var credits = await db.LedgerEntries.Where(e => e.LedgerAccountId == acc.Id && e.Direction == LedgerEntryDirection.Credit && e.LedgerTransaction.Status == LedgerTransactionStatus.Completed).SumAsync(e => e.Amount, token);

                                    fundBalance = debits - credits;

                                    balanceVerified = true;

                                }

                            }



                            linkedFund = new A5LinkedFundOverview(

                                fund.Id,

                                fund.Name,

                                true,

                                "VND",

                                fundBalance,

                                fund.FounderUserId == ticket.LinkedUserId ? "Founder" : (membership?.CanSpend == true ? "Spender" : "Member"),

                                memberCount,

                                totalContrib,

                                0

                            );

                        }

                    }



                    // Check Group Fund

                    if (linkedFund == null)

                    {

                        var groupContrib = await db.GroupContributions.AsNoTracking().SingleOrDefaultAsync(c => c.Id == busId, token);

                        if (groupContrib != null)

                        {

                            var sharedFund = await db.SharedFunds.AsNoTracking().SingleOrDefaultAsync(f => f.GroupId == groupContrib.GroupId, token);

                            var group = await db.Groups.AsNoTracking().SingleOrDefaultAsync(g => g.Id == groupContrib.GroupId, token);

                            if (sharedFund != null && group != null)

                            {

                                var memberCount = await db.GroupMembers.CountAsync(m => m.GroupId == group.Id, token);

                                var totalContrib = await db.GroupContributions.Where(c => c.GroupId == group.Id).SumAsync(c => c.Amount, token);

                                var totalExp = await db.GroupExpenses.Where(e => e.GroupId == group.Id && e.Status == GroupExpenseStatus.Recorded).SumAsync(e => e.Amount, token);

                                var gm = await db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == ticket.LinkedUserId, token);



                                linkedFund = new A5LinkedFundOverview(

                                    group.Id,

                                    group.Name,

                                    false,

                                    sharedFund.Currency,

                                    sharedFund.Balance,

                                    gm?.Role.ToString() ?? "Member",

                                    memberCount,

                                    totalContrib,

                                    totalExp

                                );

                            }

                        }

                    }

                }

            }



            audit.Add(actor, "A5_FUND_DOSSIER_READ", "SupportCase", caseId.ToString(), purpose,

                new { HasLinkedFund = linkedFund != null, DisputeCount = disputes.Count, TimelineEvents = timeline.Count });

            await db.SaveChangesAsync(token);



            return new A5FundDossierResult(

                ticket.Id,

                ticket.TicketNumber,

                ticket.LinkedUserId,

                user.FullName,

                user.Email,

                ticket.Status.ToString(),

                ticket.Category,

                ticket.Priority,

                ticket.LinkedTransactionId,

                linkedFund,

                disputes,

                timeline,

                attachments,

                "READ_ONLY_DOSSIER; Historical records and snapshots are immutable; monetary changes and direct debit/credit are prohibited."

                + (linkedFund != null && linkedFund.IsV1Fund && !balanceVerified ? " [NOTICE: Fund treasury ledger account was unverified or missing; balance may be unconfirmed]" : "")

            );

        }, ct);

    }



    // Keep the legacy route as an explicit denial without mutating historical cases.

    public Task<A5EscalationStatusResult> EscalateAsync(ClaimsPrincipal principal, Guid caseId, A5EscalateRequest request, CancellationToken ct) =>

        throw new ForbiddenException("escalation_disabled", "SuperAdmin escalation is unavailable in V1.0.");



    // A5.6 - Đã giải quyết (Resolve or Reject)

    public Task<A5ResolveResult> ResolveAsync(ClaimsPrincipal principal, Guid caseId, A5ResolveRequest request, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(request.Reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);

        var resolutionMessage = InputBoundary.RequiredText(request.ResolutionMessage, 2000, "ResolutionMessage");



        if (resolutionMessage.Length < 5)

            throw new AppException("resolution_message_required", "A concrete message of at least 5 characters is required.");



        var nextStatus = request.IsRejected ? CaseStatus.Rejected : CaseStatus.Resolved;



        return workflow.WithCaseAsync(principal, caseId, purpose, false, async (ticket, actor, token) =>

        {

            if (ticket.AssignedAdminId != null && ticket.AssignedAdminId != actor)

                throw new ForbiddenException("case_access_denied", "Assigned Admin must execute the case decision.");



            CaseWorkflowMappings.EnsureSupportTransition(ticket.Status, nextStatus);



            var oldStatus = ticket.Status.ToString();

            ticket.Status = nextStatus;

            ticket.UpdatedAt = clock.UtcNow;

            ticket.LatestReplyAt = clock.UtcNow;



            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);

            db.CaseHistories.Add(new CaseHistory

            {

                CaseId = caseId,

                CaseKind = CaseKind.Support,

                ActorId = actor,

                ActorDisplayNameSnapshot = actorName,

                ActorRole = "Admin",

                Action = "status_change",

                Message = resolutionMessage,

                OldStatus = oldStatus,

                NewStatus = nextStatus.ToString()

            });



            if (ticket.Category == "V1FundDispute" && ticket.LinkedTransactionId is { } fundRequestId)

            {

                var fundId = await db.V1FundRequests.AsNoTracking().Where(r => r.Id == fundRequestId)
                    .Select(r => (Guid?)r.FundId).SingleOrDefaultAsync(token);
                // Serialize with cancellation and payout before reading the request state.
                if (fundId is null || await db.V1Funds.Where(f => f.Id == fundId && f.DissolvedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.UpdatedAt, f => f.UpdatedAt), token) != 1)
                    throw new ConflictException("disputed_fund_unavailable", "Active disputed fund required.");
                var disputedRequest = await db.V1FundRequests.SingleOrDefaultAsync(r => r.Id == fundRequestId, token);
                if (disputedRequest is not null && db is DbContext tracked) await tracked.Entry(disputedRequest).ReloadAsync(token);

                if (disputedRequest is null || disputedRequest.State != "Suspended")

                {

                    throw new ConflictException("disputed_request_not_suspended", "Disputed request must be in Suspended state to be resolved.");

                }

                    var disputedFund = await db.V1Funds.SingleOrDefaultAsync(f => f.Id == disputedRequest.FundId, token);

                    if (nextStatus == CaseStatus.Resolved)

                    {

                        // Valid report -> Cancel expense, release hold to fund available balance

                        disputedRequest.State = "Cancelled";

                        disputedRequest.CancelledAt = clock.UtcNow;

                        disputedRequest.ReviewReason = $"Admin resolved dispute as valid: {resolutionMessage}";



                        if (disputedFund is not null)

                        {

                            db.V1FundActivities.Add(new Domain.V1Groups.V1FundActivity

                            {

                                FundId = disputedFund.Id,

                                ActorUserId = actor,

                                SubjectUserId = disputedRequest.RequestedByUserId,

                                Event = "FundDisputeResolvedCancelled",

                                Detail = $"Khoản chi {disputedRequest.AmountVnd:N0} đ được Admin hủy do báo cáo hợp lệ. Tiền trở lại quỹ. Lý do: {resolutionMessage}",

                                ReferenceId = disputedRequest.Id,

                                CreatedAt = clock.UtcNow

                            });

                        }

                    }

                    else if (nextStatus == CaseStatus.Rejected)

                    {

                        // Invalid report -> Unsuspend back to Pending24h, preserve CreatedAt, no auto-payout

                        disputedRequest.State = "Pending24h";

                        disputedRequest.ReviewReason = $"Admin rejected dispute report: {resolutionMessage}";



                        if (disputedFund is not null)

                        {

                            db.V1FundActivities.Add(new Domain.V1Groups.V1FundActivity

                            {

                                FundId = disputedFund.Id,

                                ActorUserId = actor,

                                SubjectUserId = disputedRequest.RequestedByUserId,

                                Event = "FundDisputeRejectedResumed",

                                Detail = $"Báo cáo khoản chi {disputedRequest.AmountVnd:N0} đ bị bác bỏ. Khoản chi tiếp tục quy trình chờ duyệt. Lý do: {resolutionMessage}",

                                ReferenceId = disputedRequest.Id,

                                CreatedAt = clock.UtcNow

                            });

                        }

                    }

            }



            audit.Add(actor, "A5_CASE_" + nextStatus.ToString().ToUpperInvariant(), "SupportCase", caseId.ToString(), purpose,

                new { OldStatus = oldStatus, NewStatus = nextStatus.ToString() });

            await db.SaveChangesAsync(token);



            var owner = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ticket.LinkedUserId, token);

            if (nextStatus == CaseStatus.Resolved)

                await supportEmails.QueueResolvedAsync(owner, ticket, resolutionMessage, token);

            else if (nextStatus == CaseStatus.Rejected)

                await supportEmails.QueueRejectedAsync(owner, ticket, resolutionMessage, token);



            await notifications.CreateAsync(ticket.LinkedUserId, "SupportUpdated",

                nextStatus == CaseStatus.Resolved ? "Yêu cầu hỗ trợ đã giải quyết" : "Yêu cầu hỗ trợ bị từ chối",

                resolutionMessage,

                new { supportCaseId = caseId, status = nextStatus.ToString().ToLowerInvariant(), actionRoute = $"/support/{caseId}" },

                token);



            return new A5ResolveResult(

                ticket.Id,

                ticket.TicketNumber,

                oldStatus,

                nextStatus.ToString(),

                resolutionMessage,

                clock.UtcNow

            );

        }, ct);

    }



    public async Task<A5ResolveResult> ResolveDisputeRequestAsync(ClaimsPrincipal principal, Guid requestId, A5ResolveRequest request, CancellationToken ct)
    {
        var purpose = InputBoundary.RequiredText(request.Reason, 1000, "Reason");
        AdminAuditWriter.RequireReason(purpose);
        var resolutionMessage = InputBoundary.RequiredText(request.ResolutionMessage, 2000, "ResolutionMessage");
        if (resolutionMessage.Length < 5)
            throw new AppException("resolution_message_required", "A concrete message of at least 5 characters is required.");

        var ticket = await db.SupportCases.SingleOrDefaultAsync(c => c.Category == "V1FundDispute" && c.LinkedTransactionId == requestId, ct)
            ?? throw new NotFoundException("dispute_case_not_found", "No support case found for this disputed request.");

        return await ResolveAsync(principal, ticket.Id, request, ct);
    }

    // A5.7 - Mẫu phản hồi: Danh mục mẫu chuẩn

    public async Task<IReadOnlyList<A5SupportTemplateItem>> GetTemplatesAsync(ClaimsPrincipal principal, string reason, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);



        if (!await permissions.HasAsync(principal, PlatformPermission.OperationsRead, ct))

            throw new ForbiddenException("permission_denied", "Operator required.");



        IReadOnlyList<A5SupportTemplateItem> catalog =

        [

            new("support-info-request", "Yêu cầu bổ sung sao kê/chứng từ", "Verification",

                "Yêu cầu người dùng cung cấp thêm hóa đơn hoặc sao kê ngân hàng đối chiếu.",

                ["TicketNumber", "Subject", "RequiredDocuments", "Note"]),



            new("support-resolved", "Thông báo đã giải quyết khiếu nại", "Resolution",

                "Thông báo kết quả giải quyết hoàn tất cho người dùng.",

                ["TicketNumber", "Message", "ActionUrl"]),



            new("support-rejected", "Thông báo từ chối yêu cầu hỗ trợ", "Resolution",

                "Thông báo lý do từ chối hỗ trợ theo chính sách hệ thống.",

                ["TicketNumber", "Message", "ActionUrl"]),



            new("support-fund-investigation", "Thông báo tiến độ điều tra quỹ", "FundInvestigation",

                "Cập nhật người dùng về việc hồ sơ quỹ đang trong quá trình rà soát đối chiếu sổ cái.",

                ["TicketNumber", "FundName", "CurrentPhase", "Note"])

        ];



        return catalog;

    }



    // A5.7 - Áp dụng mẫu phản hồi gửi cho User

    public Task<A5AppliedTemplateResult> ApplyTemplateAsync(ClaimsPrincipal principal, Guid caseId, A5ApplyTemplateRequest request, CancellationToken ct)

    {

        var purpose = InputBoundary.RequiredText(request.Reason, 1000, "Reason");

        AdminAuditWriter.RequireReason(purpose);

        var tplKey = InputBoundary.RequiredText(request.TemplateKey, 100, "TemplateKey");



        // Validate forbidden injection: templates must not contain secrets or tokens

        foreach (var (k, v) in request.Parameters)

        {

            if (v != null && (v.Contains("password", StringComparison.OrdinalIgnoreCase)

                || v.Contains("secret", StringComparison.OrdinalIgnoreCase)

                || v.Contains("token", StringComparison.OrdinalIgnoreCase)))

            {

                throw new AppException("invalid_template_parameter", "Template parameters must not contain sensitive authentication data.");

            }

        }



        return workflow.WithCaseAsync(principal, caseId, purpose, false, async (ticket, actor, token) =>

        {

            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == ticket.LinkedUserId, token);



            // Populate base parameters

            var finalParams = new Dictionary<string, string?>(request.Parameters, StringComparer.OrdinalIgnoreCase)

            {

                ["TicketNumber"] = ticket.TicketNumber,

                ["Subject"] = ticket.Subject,

                ["UserName"] = user.FullName

            };



            var rendered = templateRenderer.Render(tplKey, finalParams);

            var subjectLine = $"[SpendWise] Cập nhật yêu cầu {ticket.TicketNumber}";



            var oldStatus = ticket.Status.ToString();

            if (request.TransitionToWaitingForUser && ticket.Status != CaseStatus.WaitingForUser)

            {

                ticket.Status = CaseStatus.WaitingForUser;

                ticket.UpdatedAt = clock.UtcNow;

            }

            ticket.LatestReplyAt = clock.UtcNow;



            var actorName = await db.Users.AsNoTracking().Where(u => u.Id == actor).Select(u => u.FullName).SingleAsync(token);

            db.CaseHistories.Add(new CaseHistory

            {

                CaseId = caseId,

                CaseKind = CaseKind.Support,

                ActorId = actor,

                ActorDisplayNameSnapshot = actorName,

                ActorRole = "Admin",

                Action = "applied_template:" + tplKey,

                Message = rendered.TextBody,

                OldStatus = oldStatus,

                NewStatus = ticket.Status.ToString()

            });



            audit.Add(actor, "A5_APPLY_TEMPLATE", "SupportCase", caseId.ToString(), purpose,

                new { TemplateKey = tplKey, CaseStatus = ticket.Status.ToString() });

            await db.SaveChangesAsync(token);



            await notifications.CreateAsync(ticket.LinkedUserId, "SupportUpdated", subjectLine,

                rendered.TextBody[..Math.Min(200, rendered.TextBody.Length)],

                new { supportCaseId = caseId, status = ticket.Status.ToString().ToLowerInvariant(), actionRoute = $"/support/{caseId}" },

                token);



            return new A5AppliedTemplateResult(

                ticket.Id,

                tplKey,

                subjectLine,

                rendered.TextBody,

                true,

                ticket.Status.ToString()

            );

        }, ct);

    }



    private static void ValidatePage(int page, int pageSize)

    {

        InputBoundary.Page(page, pageSize);

        if (page - 1 > int.MaxValue / pageSize)

            throw new AppException("invalid_pagination", "Page too large.");

    }



    private static void ValidateDateRange(DateTime? from, DateTime? to)

    {

        if (from is { } f && f.Kind != DateTimeKind.Utc || to is { } t && t.Kind != DateTimeKind.Utc || from >= to)

            throw new AppException("invalid_date_range", "Use increasing UTC boundaries.");

    }

}

