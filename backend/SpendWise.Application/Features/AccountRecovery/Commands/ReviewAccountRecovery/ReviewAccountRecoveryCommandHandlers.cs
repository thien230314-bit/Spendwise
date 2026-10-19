using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin;
using SpendWise.Application.Features.Authentication;
using SpendWise.Application.Identity;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.AccountRecovery.Commands.ReviewAccountRecovery;

public sealed class RequestMoreAccountRecoveryInfoCommandHandler(
    ISpendWiseDbContext db,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    INotificationService notifications,
    IClock clock)
    : IRequestHandler<RequestMoreAccountRecoveryInfoCommand, AccountRecoveryDto>
{
    public async Task<AccountRecoveryDto> Handle(RequestMoreAccountRecoveryInfoCommand command, CancellationToken ct)
    {
        await security.VerifyAsync(command.AdminId, command.Request.AdminPin, ct);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var request = await RecoveryReviewSupport.LoadMutableAsync(db, command.RequestId, token);
            RecoveryReviewSupport.EnsureOpen(request);
            if (request.ResolvedUserId is Guid subjectId)
                await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, subjectId, token);
            db.AccountRecoveryReviews.Add(RecoveryReviewSupport.ApplyReview(request, command.AdminId, "MORE_INFO_NEEDED", command.Request.Reason, AccountRecoveryStatus.MoreInfoNeeded, clock.UtcNow));
            audit.Add(command.AdminId, "ACCOUNT_RECOVERY_MORE_INFO", "AccountRecoveryRequest", request.Id.ToString(), command.Request.Reason, null);
            await db.SaveChangesAsync(token);
            if (request.ResolvedUserId is Guid userId)
            {
                await notifications.CreateAsync(userId, "SecurityAlert", "More information required", "More information is required for your account recovery request.", new { request.Id }, token);
            }
            return AccountRecoveryMappings.ToDto(request);
        }, ct);
    }
}

public sealed class RejectAccountRecoveryCommandHandler(
    ISpendWiseDbContext db,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    INotificationService notifications,
    IClock clock)
    : IRequestHandler<RejectAccountRecoveryCommand, AccountRecoveryDto>
{
    public async Task<AccountRecoveryDto> Handle(RejectAccountRecoveryCommand command, CancellationToken ct)
    {
        await security.VerifyAsync(command.AdminId, command.Request.AdminPin, ct);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            var request = await RecoveryReviewSupport.LoadMutableAsync(db, command.RequestId, token);
            RecoveryReviewSupport.EnsureOpen(request);
            if (request.ResolvedUserId is Guid subjectId)
                await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, subjectId, token);
            db.AccountRecoveryReviews.Add(RecoveryReviewSupport.ApplyReview(request, command.AdminId, "REJECTED", command.Request.Reason, AccountRecoveryStatus.Rejected, clock.UtcNow));
            audit.Add(command.AdminId, "REJECT_ACCOUNT_RECOVERY", "AccountRecoveryRequest", request.Id.ToString(), command.Request.Reason, null);
            await db.SaveChangesAsync(token);
            if (request.ResolvedUserId is Guid userId)
            {
                await notifications.CreateAsync(userId, "SecurityAlert", "Recovery request reviewed", "Your account recovery request was not approved.", new { request.Id }, token);
            }
            return AccountRecoveryMappings.ToDto(request);
        }, ct);
    }
}

public sealed class ApproveAccountRecoveryCommandHandler(
    ISpendWiseDbContext db,
    AdminSecurityGuard security,
    AdminAuditWriter audit,
    ITemporaryPasswordService temporaryPasswords,
    IPasswordService passwords,
    AuthenticationSupport authentication,
    INotificationService notifications,
    IClock clock)
    : IRequestHandler<ApproveAccountRecoveryCommand, AccountRecoveryApprovalResponse>
{
    public async Task<AccountRecoveryApprovalResponse> Handle(ApproveAccountRecoveryCommand command, CancellationToken ct)
    {
        await security.VerifyAsync(command.AdminId, command.Request.AdminPin, ct);
        AdminAuditWriter.RequireReason(command.Request.Reason);
        var temporaryPassword = temporaryPasswords.CreateStrongPassword();
        AuthenticationSupport.ValidatePassword(temporaryPassword);
        var recovery = await db.ExecuteInTransactionAsync(async token =>
        {
            var request = await RecoveryReviewSupport.LoadMutableAsync(db, command.RequestId, token);
            RecoveryReviewSupport.EnsureOpen(request);
            foreach (var id in new[] { request.ResolvedUserId, (Guid?)command.Request.UserId }
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().Order())
                await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, id, token);
            var user = await AdminSecurityGuard.RequireOrdinaryTargetAsync(db, command.Request.UserId, token);

            user.PasswordHash = passwords.Hash(temporaryPassword);
            user.MustChangePassword = true;
            user.PasswordFailedAttempts = 0;
            user.PasswordLockoutUntil = null;
            await authentication.RevokeAllSessionsAsync(user.Id, token);
            request.ResolvedUserId = user.Id;
            db.AccountRecoveryReviews.Add(RecoveryReviewSupport.ApplyReview(request, command.AdminId, "APPROVED", command.Request.Reason, AccountRecoveryStatus.Approved, clock.UtcNow));
            audit.Add(command.AdminId, "APPROVE_ACCOUNT_RECOVERY", "AccountRecoveryRequest", request.Id.ToString(), command.Request.Reason, new { UserId = user.Id, MustChangePassword = true });
            await db.SaveChangesAsync(token);
            await notifications.CreateAsync(user.Id, "SecurityAlert", "Recovery request approved", "Your recovery request was approved. Use the temporary credential provided through the secure support channel.", new { request.Id }, token);
            return AccountRecoveryMappings.ToDto(request);
        }, ct);
        return new AccountRecoveryApprovalResponse(recovery, temporaryPassword);
    }
}

internal static class RecoveryReviewSupport
{
    public static async Task<AccountRecoveryRequest> LoadMutableAsync(ISpendWiseDbContext db, Guid id, CancellationToken ct) =>
        await db.AccountRecoveryRequests.Include(item => item.Reviews).SingleOrDefaultAsync(item => item.Id == id, ct)
        ?? throw new NotFoundException("recovery_request_not_found", "Account recovery request not found.");

    public static void EnsureOpen(AccountRecoveryRequest request)
    {
        if (request.Status is AccountRecoveryStatus.Approved or AccountRecoveryStatus.Rejected)
        {
            throw new ConflictException("recovery_request_terminal", "Completed recovery requests cannot be changed.");
        }
    }

    public static AccountRecoveryReview ApplyReview(AccountRecoveryRequest request, Guid adminId, string action, string reason, AccountRecoveryStatus status, DateTime reviewedAt)
    {
        request.Status = status;
        request.ReviewedByAdminId = adminId;
        request.ReviewedAt = reviewedAt;
        request.AdminNote = reason.Trim();
        var review = new AccountRecoveryReview
        {
            AccountRecoveryRequestId = request.Id,
            Request = request,
            AdminId = adminId,
            Action = action,
            Note = reason.Trim()
        };
        request.Reviews.Add(review);
        return review;
    }
}
