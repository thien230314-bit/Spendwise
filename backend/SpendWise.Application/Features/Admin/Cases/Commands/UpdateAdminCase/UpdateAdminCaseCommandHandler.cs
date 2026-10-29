using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.UpdateAdminCase;

// Handler cập nhật đúng collection RiskCase, Dispute hoặc SupportCase theo loại đã xác thực.

public sealed class UpdateAdminCaseCommandHandler(
    ISpendWiseDbContext db,
    AdminAuditWriter audit,
    IClock clock,
    SupportCaseEmailSupport supportEmails,
    INotificationService notifications) : IRequestHandler<UpdateAdminCaseCommand, CaseDto>
{
    public async Task<CaseDto> Handle(
        UpdateAdminCaseCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ ba loại case công khai được phép đi tiếp vào workflow cập nhật.
        var normalizedType = command.Type.Trim().ToLowerInvariant();
        if (normalizedType is not ("risk" or "disputes" or "support"))
        {
            throw new AppException(
                "invalid_case_type",
                "Case type must be risk, disputes, or support.");
        }

        AdminAuditWriter.RequireReason(command.Request.Reason);
        var internalNotes = InputBoundary.OptionalText(command.Request.InternalNotes, 4000, "Internal notes");
        if (normalizedType == "support")
        {
            return await UpdateSupportAsync(command, internalNotes, cancellationToken);
        }

        CaseDto result;
        if (normalizedType == "risk")
        {
            if (!Enum.TryParse<ReviewStatus>(command.Request.Status, true, out var reviewStatus)
                || !Enum.IsDefined(reviewStatus))
            {
                throw new AppException(
                    "invalid_case_status",
                    "Risk case status must be Pending, Approved, or Rejected.");
            }

            // Duyệt RiskCase chỉ cập nhật hồ sơ, không tự chạy lại transfer.
            var riskCase = await db.RiskCases.FindAsync(
                [command.CaseId],
                cancellationToken)
                ?? throw new NotFoundException(
                    "case_not_found",
                    "Risk case not found.");
            riskCase.ReviewStatus = reviewStatus;
            riskCase.ReviewedByAdminId = command.AdminId;
            result = AdminMappings.ToRiskCaseDto(riskCase);
        }
        else
        {
            if (!Enum.TryParse<CaseStatus>(command.Request.Status, true, out var caseStatus)
                || !Enum.IsDefined(caseStatus))
            {
                throw new AppException(
                    "invalid_case_status",
                    "Dispute status is invalid.");
            }

            var dispute = await db.Disputes.FindAsync(
                [command.CaseId],
                cancellationToken)
                ?? throw new NotFoundException(
                    "case_not_found",
                    "Dispute not found.");
            dispute.Status = caseStatus;
            dispute.InternalNotes = internalNotes;
            result = AdminMappings.ToDisputeDto(dispute);
        }
        audit.Add(
            command.AdminId,
            "UPDATE_CASE",
            normalizedType,
            command.CaseId.ToString(),
            command.Request.Reason,
            new { command.Request.Status });
        await db.SaveChangesAsync(cancellationToken);

        return result;
    }

    private Task<CaseDto> UpdateSupportAsync(
        UpdateAdminCaseCommand command,
        string? internalNotes,
        CancellationToken cancellationToken)
    {
        // Kiểm tra contract trước khi truy vấn để lỗi status luôn nhất quán.
        var next = CaseWorkflowMappings.ParseSupport(command.Request.Status);
        var message = CaseWorkflowMappings.SupportStatusMessage(next, command.Request.Reason);
        return db.ExecuteInTransactionAsync(async token =>
        {
            var current = await db.SupportCases.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == command.CaseId, token)
                ?? throw new NotFoundException("case_not_found", "Support case not found.");
            CaseWorkflowMappings.EnsureSupportTransition(current.Status, next);
            var oldStatus = CaseWorkflowMappings.Status(current.Status);
            var newStatus = CaseWorkflowMappings.Status(next);

            // Cập nhật có điều kiện để hai Admin không thể cùng hoàn tất một case.
            var affected = await db.SupportCases
                .Where(x => x.Id == current.Id && x.Status == current.Status)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, next)
                    .SetProperty(x => x.InternalNotes, internalNotes)
                    .SetProperty(x => x.AssignedAdminId, command.AdminId)
                    .SetProperty(x => x.UpdatedAt, clock.UtcNow), token);
            if (affected != 1)
                throw new ConflictException("concurrent_case_update", "Support case status changed. Refresh and retry.");

            var adminName = await db.Users.AsNoTracking()
                .Where(x => x.Id == command.AdminId)
                .Select(x => x.FullName)
                .SingleAsync(token);
            db.CaseHistories.Add(new CaseHistory
            {
                CaseKind = CaseKind.Support,
                CaseId = current.Id,
                ActorId = command.AdminId,
                ActorDisplayNameSnapshot = adminName,
                ActorRole = "Admin",
                Action = next == CaseStatus.WaitingForUser ? "request_info" : "status_change",
                Message = message,
                OldStatus = oldStatus,
                NewStatus = newStatus
            });
            audit.Add(
                command.AdminId,
                "UPDATE_CASE",
                "support",
                current.Id.ToString(),
                command.Request.Reason,
                new { command.Request.Status });
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

            var updated = await db.SupportCases.AsNoTracking()
                .SingleAsync(x => x.Id == current.Id, token);
            return AdminMappings.ToSupportCaseDto(updated);
        }, cancellationToken);
    }
}
