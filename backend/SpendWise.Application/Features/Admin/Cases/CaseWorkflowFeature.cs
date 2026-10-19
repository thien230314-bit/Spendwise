using System.Security.Cryptography;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Admin.Cases;

public sealed record GetSupportCasesQuery(Guid? UserId) : IRequest<IReadOnlyList<SupportCaseDetailDto>>;
public sealed record GetSupportCaseQuery(Guid? UserId, Guid CaseId) : IRequest<SupportCaseDetailDto>;
public sealed record AddSupportMessageCommand(Guid ActorId, bool IsAdmin, Guid CaseId, CaseMessageRequest Request) : IRequest<SupportCaseDetailDto>;
public sealed record UpdateSupportStatusCommand(Guid AdminId, Guid CaseId, CaseStatusRequest Request) : IRequest<SupportCaseDetailDto>;
public sealed record GrantSupportCorrectionCommand(Guid AdminId, Guid CaseId, CorrectionGrantRequest Request) : IRequest<SupportCaseDetailDto>;
public sealed record GetDisputesQuery(Guid? UserId) : IRequest<IReadOnlyList<DisputeDetailDto>>;
public sealed record GetDisputeQuery(Guid? UserId, Guid CaseId) : IRequest<DisputeDetailDto>;
public sealed record AddDisputeMessageCommand(Guid ActorId, bool IsAdmin, Guid CaseId, CaseMessageRequest Request) : IRequest<DisputeDetailDto>;
public sealed record UpdateDisputeStatusCommand(Guid AdminId, Guid CaseId, CaseStatusRequest Request) : IRequest<DisputeDetailDto>;
public sealed record GetCaseAttachmentQuery(Guid ActorId, bool IsAdmin, Guid AttachmentId) : IRequest<AttachmentContentDto>;

public sealed class CaseAttachmentValidator
{
    public const int MaximumFiles = 5;
    public const int MaximumFileSize = 5 * 1024 * 1024;
    public IReadOnlyList<CaseAttachment> Create(Guid ownerId, Guid historyId, IReadOnlyList<CaseAttachmentUpload>? uploads)
    {
        if (uploads is null) return [];
        ValidateCount(uploads.Count);
        return uploads.Select(upload => CreateOne(ownerId, historyId, upload)).ToList();
    }
    public static void ValidateCount(int count)
    {
        if (count > MaximumFiles) throw new AppException("attachment_limit", "At most 5 attachments are allowed.");
    }
    public static void ValidateSize(long length)
    {
        if (length is 0 or > MaximumFileSize) throw new AppException("attachment_size", "Attachment must be between 1 byte and 5 MB.");
    }
    private static CaseAttachment CreateOne(Guid ownerId, Guid historyId, CaseAttachmentUpload upload)
    {
        ValidateSize(upload.Content.LongLength);
        var safeName = Path.GetFileName(upload.FileName);
        if (safeName != upload.FileName || safeName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new AppException("attachment_name", "Attachment filename is invalid.");
        var ext = Path.GetExtension(safeName).ToLowerInvariant();
        var mime = upload.ContentType.ToLowerInvariant();
        var valid = (ext is ".jpg" or ".jpeg") && mime == "image/jpeg" && upload.Content.Length >= 3 && upload.Content[0] == 0xFF && upload.Content[1] == 0xD8 && upload.Content[2] == 0xFF
            || ext == ".png" && mime == "image/png" && upload.Content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47 })
            || ext == ".webp" && mime == "image/webp" && upload.Content.Length >= 12 && System.Text.Encoding.ASCII.GetString(upload.Content, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(upload.Content, 8, 4) == "WEBP"
            || ext == ".pdf" && mime == "application/pdf" && upload.Content.AsSpan().StartsWith("%PDF"u8);
        if (!valid) throw new AppException("attachment_type", "Attachment type or signature is invalid.");
        return new CaseAttachment { CaseHistoryId = historyId, CaseOwnerUserId = ownerId, OriginalFileName = safeName,
            ContentType = mime, Length = upload.Content.LongLength, ContentHash = Convert.ToHexString(SHA256.HashData(upload.Content)), Content = upload.Content };
    }
}

internal static class CaseWorkflowMappings
{
    public static string Status(CaseStatus status) => status switch { CaseStatus.New => "pending", CaseStatus.Open => "open", CaseStatus.Investigating => "in_progress", CaseStatus.WaitingForUser => "need_more_info", CaseStatus.Resolved => "resolved", CaseStatus.Rejected => "rejected", CaseStatus.Closed => "closed", _ => status.ToString().ToLowerInvariant() };
    public static CaseStatus ParseSupport(string value) => value.Trim().ToLowerInvariant() switch { "pending" or "new" => CaseStatus.New, "open" => CaseStatus.Open, "in_progress" or "investigating" => CaseStatus.Investigating, "need_more_info" or "waitingforuser" or "waiting_for_user" => CaseStatus.WaitingForUser, "resolved" => CaseStatus.Resolved, "rejected" => CaseStatus.Rejected, "closed" => CaseStatus.Closed, _ => throw new AppException("invalid_case_status", "Support status is invalid.") };
    public static CaseStatus ParseDispute(string value) => value.Trim().ToLowerInvariant() switch { "pending" or "submitted" => CaseStatus.New, "under_review" or "in_review" => CaseStatus.Investigating, "need_more_info" => CaseStatus.WaitingForUser, "resolved" => CaseStatus.Resolved, "rejected" => CaseStatus.Rejected, _ => throw new AppException("invalid_case_status", "Dispute status is invalid.") };
    public static IReadOnlyList<CaseHistoryDto> History(IEnumerable<CaseHistory> items) => items.OrderBy(x => x.CreatedAt).Select(x => new CaseHistoryDto(x.Id, x.ActorId, x.ActorDisplayNameSnapshot, x.ActorRole, x.Action, x.Message, x.OldStatus, x.NewStatus, x.CreatedAt, x.Attachments.Select(a => new CaseAttachmentDto(a.Id, a.OriginalFileName, a.ContentType, a.Length, a.ContentHash, a.CreatedAt)).ToList())).ToList();
    public static SupportCaseDetailDto Support(SupportCase item, IEnumerable<CaseHistory> history) => new(item.Id, item.TicketNumber, item.LinkedUserId, item.Subject, item.Description, item.Category, item.Priority, Status(item.Status), item.LinkedTransactionId, item.AssignedAdminId, item.CreatedAt, item.UpdatedAt, item.LatestReplyAt, item.CorrectionGranted, History(history));
    public static DisputeDetailDto Dispute(Dispute item, IEnumerable<CaseHistory> history) => new(item.Id, item.UserId, item.LinkedTransactionId, item.TransactionReferenceSnapshot, item.TransactionTitleSnapshot, item.TransactionAmountSnapshot, item.CounterpartySnapshot, item.TransactionOccurredAtSnapshot, item.Reason, item.Description, Status(item.Status), item.Resolution, item.CreatedAt, item.UpdatedAt, History(history));

    public static void EnsureSupportTransition(CaseStatus current, CaseStatus next)
    {
        if (current is CaseStatus.Resolved or CaseStatus.Rejected or CaseStatus.Closed)
            throw new ConflictException("case_closed", "Closed cases cannot transition.");
        if (next == CaseStatus.New)
            throw new ConflictException("invalid_case_transition", "A case cannot return to pending.");
    }

    public static string SupportStatusMessage(CaseStatus next, string message)
    {
        var text = InputBoundary.RequiredText(message, next is CaseStatus.Resolved or CaseStatus.Rejected ? 2000 : 4000, "Message");
        if (next == CaseStatus.Resolved && text.Length < 5)
            throw new AppException("resolution_message_required", "A concrete resolution message is required.");
        if (next == CaseStatus.Rejected && text.Length < 5)
            throw new AppException("rejection_reason_required", "A concrete rejection reason is required.");
        return text;
    }
}

public sealed class CaseWorkflowHandler(ISpendWiseDbContext db, CaseAttachmentValidator attachments, IClock clock,
    AdminSecurityGuard adminSecurity, AdminAuditWriter audit, INotificationService notifications,
    SupportCaseEmailSupport supportEmails) :
    IRequestHandler<GetSupportCasesQuery, IReadOnlyList<SupportCaseDetailDto>>, IRequestHandler<GetSupportCaseQuery, SupportCaseDetailDto>,
    IRequestHandler<AddSupportMessageCommand, SupportCaseDetailDto>, IRequestHandler<UpdateSupportStatusCommand, SupportCaseDetailDto>,
    IRequestHandler<GrantSupportCorrectionCommand, SupportCaseDetailDto>, IRequestHandler<GetDisputesQuery, IReadOnlyList<DisputeDetailDto>>,
    IRequestHandler<GetDisputeQuery, DisputeDetailDto>, IRequestHandler<AddDisputeMessageCommand, DisputeDetailDto>,
    IRequestHandler<UpdateDisputeStatusCommand, DisputeDetailDto>, IRequestHandler<GetCaseAttachmentQuery, AttachmentContentDto>
{
    public async Task<IReadOnlyList<SupportCaseDetailDto>> Handle(GetSupportCasesQuery q, CancellationToken ct)
    {
        var cases = await db.SupportCases.AsNoTracking().Where(x => q.UserId == null || x.LinkedUserId == q.UserId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var results = new List<SupportCaseDetailDto>();
        foreach (var item in cases)
            results.Add(await SupportAsync(item, ct));
        return results;
    }
    public async Task<SupportCaseDetailDto> Handle(GetSupportCaseQuery q, CancellationToken ct) { var item = await db.SupportCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.CaseId && (q.UserId == null || x.LinkedUserId == q.UserId), ct) ?? throw new NotFoundException("support_not_found", "Support case not found."); return await SupportAsync(item, ct); }
    public async Task<SupportCaseDetailDto> Handle(AddSupportMessageCommand c, CancellationToken ct) { var item = await db.SupportCases.SingleOrDefaultAsync(x => x.Id == c.CaseId && (c.IsAdmin || x.LinkedUserId == c.ActorId), ct) ?? throw new NotFoundException("support_not_found", "Support case not found."); var action = item.Status == CaseStatus.WaitingForUser && !c.IsAdmin ? "provide_info" : "reply"; await AddHistoryAsync(CaseKind.Support, item.Id, item.LinkedUserId, c.ActorId, c.IsAdmin, action, c.Request.Message, null, null, c.Request.Attachments, ct); item.LatestReplyAt = clock.UtcNow; if (!c.IsAdmin && item.Status == CaseStatus.WaitingForUser) item.Status = CaseStatus.Open; await db.SaveChangesAsync(ct); return await SupportAsync(item, ct); }
    public async Task<SupportCaseDetailDto> Handle(UpdateSupportStatusCommand c, CancellationToken ct)
    {
        var current = await db.SupportCases.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == c.CaseId, ct)
            ?? throw new NotFoundException("support_not_found", "Support case not found.");
        var next = CaseWorkflowMappings.ParseSupport(c.Request.Status);
        CaseWorkflowMappings.EnsureSupportTransition(current.Status, next);
        var message = CaseWorkflowMappings.SupportStatusMessage(next, c.Request.Message);
        var oldStatus = CaseWorkflowMappings.Status(current.Status);
        var newStatus = CaseWorkflowMappings.Status(next);

        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Compare-and-set giúp chỉ một yêu cầu cạnh tranh được chuyển trạng thái case.
            var affected = await db.SupportCases
                .Where(x => x.Id == current.Id && x.Status == current.Status)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, next)
                    .SetProperty(x => x.AssignedAdminId, c.AdminId)
                    .SetProperty(x => x.UpdatedAt, clock.UtcNow), token);
            if (affected != 1)
                throw new ConflictException("concurrent_case_update", "Support case status changed. Refresh and retry.");

            await AddHistoryAsync(
                CaseKind.Support,
                current.Id,
                current.LinkedUserId,
                c.AdminId,
                true,
                next == CaseStatus.WaitingForUser ? "request_info" : "status_change",
                message,
                oldStatus,
                newStatus,
                c.Request.Attachments,
                token);
            // Audit giữ lý do ngắn, còn nội dung đầy đủ vẫn nằm trong CaseHistory.
            var auditReason = message.Length < 5
                ? $"Support status changed to {newStatus}."
                : message[..Math.Min(message.Length, 1000)];
            audit.Add(
                c.AdminId,
                "UPDATE_CASE",
                "support",
                current.Id.ToString(),
                auditReason,
                new { status = newStatus });
            await db.SaveChangesAsync(token);

            var owner = await db.Users.AsNoTracking()
                .SingleAsync(x => x.Id == current.LinkedUserId, token);
            if (next == CaseStatus.Resolved)
                await supportEmails.QueueResolvedAsync(owner, current, message, token);
            else if (next == CaseStatus.Rejected)
                await supportEmails.QueueRejectedAsync(owner, current, message, token);

            await notifications.CreateAsync(
                current.LinkedUserId,
                "SupportUpdated",
                "Support case updated",
                message,
                new { supportCaseId = current.Id, status = newStatus, actionRoute = $"/support/{current.Id}" },
                token);
            var updated = await db.SupportCases.AsNoTracking().SingleAsync(x => x.Id == current.Id, token);
            return await SupportAsync(updated, token);
        }, ct);
    }
    public async Task<SupportCaseDetailDto> Handle(GrantSupportCorrectionCommand c, CancellationToken ct) { await adminSecurity.VerifyAsync(c.AdminId, c.Request.AdminPin, ct); return await db.ExecuteInTransactionAsync(async token => { var item = await db.SupportCases.SingleOrDefaultAsync(x => x.Id == c.CaseId, token) ?? throw new NotFoundException("support_not_found", "Support case not found."); if (item.Category != "transaction_edit" || item.LinkedTransactionId is not Guid transactionId) throw new ConflictException("correction_grant_ineligible", "Support case is not an eligible transaction-edit case."); if (item.CorrectionGranted) throw new ConflictException("correction_already_granted", "This support case already granted a correction."); var transaction = await db.FinancialTransactions.SingleOrDefaultAsync(x => x.Id == transactionId && x.OwnerUserId == item.LinkedUserId && x.IsManual && (x.Type == FinancialTransactionType.Income || x.Type == FinancialTransactionType.Expense), token) ?? throw new ConflictException("correction_grant_ineligible", "Linked transaction is not eligible."); transaction.CorrectionAllowance += 1; item.CorrectionGranted = true; item.CorrectionGrantedAt = clock.UtcNow; item.CorrectionGrantedByAdminId = c.AdminId; item.CorrectionGrantReason = InputBoundary.RequiredText(c.Request.Reason, 1000, "Reason"); audit.Add(c.AdminId, "GRANT_TRANSACTION_CORRECTION", "SupportCase", item.Id.ToString(), c.Request.Reason, new { transactionId }); await db.SaveChangesAsync(token); await notifications.CreateAsync(item.LinkedUserId, "SupportUpdated", "Correction granted", "One additional transaction correction was granted.", new { supportCaseId = item.Id, transactionId, actionRoute = $"/transactions/{transactionId}" }, token); return await SupportAsync(item, token); }, ct); }
    public async Task<IReadOnlyList<DisputeDetailDto>> Handle(GetDisputesQuery q, CancellationToken ct)
    {
        var cases = await db.Disputes.AsNoTracking().Where(x => q.UserId == null || x.UserId == q.UserId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var results = new List<DisputeDetailDto>();
        foreach (var item in cases)
            results.Add(await DisputeAsync(item, ct));
        return results;
    }
    public async Task<DisputeDetailDto> Handle(GetDisputeQuery q, CancellationToken ct) { var item = await db.Disputes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.CaseId && (q.UserId == null || x.UserId == q.UserId), ct) ?? throw new NotFoundException("dispute_not_found", "Dispute not found."); return await DisputeAsync(item, ct); }
    public async Task<DisputeDetailDto> Handle(AddDisputeMessageCommand c, CancellationToken ct) { var item = await db.Disputes.SingleOrDefaultAsync(x => x.Id == c.CaseId && (c.IsAdmin || x.UserId == c.ActorId), ct) ?? throw new NotFoundException("dispute_not_found", "Dispute not found."); var action = item.Status == CaseStatus.WaitingForUser && !c.IsAdmin ? "provide_info" : "reply"; await AddHistoryAsync(CaseKind.Dispute, item.Id, item.UserId, c.ActorId, c.IsAdmin, action, c.Request.Message, null, null, c.Request.Attachments, ct); item.LatestReplyAt = clock.UtcNow; if (!c.IsAdmin && item.Status == CaseStatus.WaitingForUser) item.Status = CaseStatus.Investigating; await db.SaveChangesAsync(ct); return await DisputeAsync(item, ct); }
    public async Task<DisputeDetailDto> Handle(UpdateDisputeStatusCommand c, CancellationToken ct) { var item = await db.Disputes.SingleOrDefaultAsync(x => x.Id == c.CaseId, ct) ?? throw new NotFoundException("dispute_not_found", "Dispute not found."); var next = CaseWorkflowMappings.ParseDispute(c.Request.Status); EnsureTransition(item.Status, next); var old = CaseWorkflowMappings.Status(item.Status); item.Status = next; if (next is CaseStatus.Resolved or CaseStatus.Rejected) item.Resolution = InputBoundary.RequiredText(c.Request.Message, 2000, "Resolution"); await AddHistoryAsync(CaseKind.Dispute, item.Id, item.UserId, c.AdminId, true, next == CaseStatus.WaitingForUser ? "request_info" : "status_change", c.Request.Message, old, CaseWorkflowMappings.Status(next), c.Request.Attachments, ct); await db.SaveChangesAsync(ct); await notifications.CreateAsync(item.UserId, "DisputeUpdated", "Dispute updated", c.Request.Message, new { disputeId = item.Id, status = CaseWorkflowMappings.Status(next), actionRoute = $"/disputes/{item.Id}" }, ct); return await DisputeAsync(item, ct); }
    public async Task<AttachmentContentDto> Handle(GetCaseAttachmentQuery q, CancellationToken ct) { var item = await db.CaseAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == q.AttachmentId && (q.IsAdmin || x.CaseOwnerUserId == q.ActorId), ct) ?? throw new NotFoundException("attachment_not_found", "Attachment not found."); return new(item.OriginalFileName, item.ContentType, item.Content); }
    private async Task AddHistoryAsync(CaseKind kind, Guid caseId, Guid ownerId, Guid actorId, bool admin, string action, string message, string? oldStatus, string? newStatus, IReadOnlyList<CaseAttachmentUpload>? uploads, CancellationToken ct) { var text = InputBoundary.RequiredText(message, 4000, "Message"); var actorName = await db.Users.Where(x => x.Id == actorId).Select(x => x.FullName).SingleAsync(ct); var history = new CaseHistory { CaseKind = kind, CaseId = caseId, ActorId = actorId, ActorDisplayNameSnapshot = actorName, ActorRole = admin ? "SystemAdmin" : "User", Action = action, Message = text, OldStatus = oldStatus, NewStatus = newStatus }; db.CaseHistories.Add(history); foreach (var file in attachments.Create(ownerId, history.Id, uploads)) db.CaseAttachments.Add(file); }
    private async Task<SupportCaseDetailDto> SupportAsync(SupportCase item, CancellationToken ct) => CaseWorkflowMappings.Support(item, await db.CaseHistories.AsNoTracking().Include(x => x.Attachments).Where(x => x.CaseKind == CaseKind.Support && x.CaseId == item.Id).ToListAsync(ct));
    private async Task<DisputeDetailDto> DisputeAsync(Dispute item, CancellationToken ct) => CaseWorkflowMappings.Dispute(item, await db.CaseHistories.AsNoTracking().Include(x => x.Attachments).Where(x => x.CaseKind == CaseKind.Dispute && x.CaseId == item.Id).ToListAsync(ct));
    private static void EnsureTransition(CaseStatus current, CaseStatus next) { if (current is CaseStatus.Resolved or CaseStatus.Rejected or CaseStatus.Closed) throw new ConflictException("case_closed", "Closed cases cannot transition."); if (next == CaseStatus.New) throw new ConflictException("invalid_case_transition", "A case cannot return to pending."); }
}
