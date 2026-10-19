using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AccountRecovery;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.AccountRecovery.Commands.SubmitAccountRecovery;

public sealed class SubmitAccountRecoveryCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<SubmitAccountRecoveryCommand, AccountRecoverySubmissionResponse>
{
    public async Task<AccountRecoverySubmissionResponse> Handle(
        SubmitAccountRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        var fullName = InputBoundary.RequiredText(command.Request.FullName, 160, "Full name");
        var phone = InputBoundary.NormalizePhone(command.Request.Phone);
        var description = InputBoundary.RequiredText(command.Request.Description, 2000, "Description");
        var emailHint = InputBoundary.OptionalText(command.Request.EmailHint, 256, "Email hint");
        if (!string.IsNullOrWhiteSpace(emailHint) && !InputBoundary.IsValidEmail(emailHint))
        {
            throw new AppException("invalid_email", "Email hint format is invalid.");
        }

        // Hệ thống tự liên kết tài khoản khi dữ liệu khớp nhưng response không tiết lộ kết quả.
        var candidate = await db.Users.AsNoTracking().SingleOrDefaultAsync(
            item => item.Phone == phone,
            cancellationToken);
        var resolvedUserId = candidate is not null
            && (emailHint is null || string.Equals(candidate.Email, emailHint, StringComparison.OrdinalIgnoreCase))
                ? candidate.Id
                : (Guid?)null;

        db.AccountRecoveryRequests.Add(new AccountRecoveryRequest
        {
            FullName = fullName,
            Phone = phone,
            Description = description,
            EmailHint = emailHint,
            ResolvedUserId = resolvedUserId
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AccountRecoverySubmissionResponse(
            "Your recovery request has been received and will be reviewed.");
    }
}
