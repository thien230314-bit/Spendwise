using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;

// Handler mở yêu cầu hỗ trợ gắn với người dùng và nội dung cần xử lý.

public sealed class CreateSupportCaseCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    SupportCaseEmailSupport supportEmails) : IRequestHandler<CreateSupportCaseCommand, CaseDto>
{
    public async Task<CaseDto> Handle(
        CreateSupportCaseCommand command,
        CancellationToken cancellationToken)
    {
        // Tạo hồ sơ hỗ trợ gắn với người dùng và giao dịch liên quan nếu có.
        var category = InputBoundary.RequiredText(command.Request.Category, 80, "Support category").ToLowerInvariant();
        var allowedCategories = new[] { "system_error", "account", "banking_sepay", "transaction_edit", "other",
            "transaction", "feature_request", "general", "complaint", "complaint_transaction", "complaint_banking",
            "complaint_fund", "complaint_account", "v1funddispute" };
        if (!allowedCategories.Contains(category))
        {
            throw new AppException("invalid_support_category", "Support category is invalid.");
        }
        var priority = InputBoundary.RequiredText(command.Request.Priority, 20, "Support priority");
        var subject = InputBoundary.RequiredText(command.Request.Subject, 200, "Subject");
        // Client cũ chỉ gửi Category/Priority; Subject là nội dung an toàn để khởi tạo hội thoại.
        var description = string.IsNullOrWhiteSpace(command.Request.Description)
            ? subject
            : InputBoundary.RequiredText(command.Request.Description, 4000, "Description");
        if (command.Request.LinkedTransactionId is Guid linkedTransactionId)
        {
            // Chỉ cho gắn giao dịch tài chính thuộc chính người tạo support case.
            var ownsTransaction = await db.FinancialTransactions.AnyAsync(
                transaction => transaction.Id == linkedTransactionId
                    && transaction.OwnerUserId == command.UserId,
                cancellationToken);
            if (!ownsTransaction)
            {
                throw new NotFoundException(
                    "transaction_not_found",
                    "Transaction not found.");
            }

            if (category == "transaction_edit")
            {
                var eligible = await db.FinancialTransactions.AnyAsync(transaction =>
                    transaction.Id == linkedTransactionId
                    && transaction.OwnerUserId == command.UserId
                    && transaction.IsManual
                    && (transaction.Type == SpendWise.Domain.Finance.FinancialTransactionType.Income
                        || transaction.Type == SpendWise.Domain.Finance.FinancialTransactionType.Expense),
                    cancellationToken);
                if (!eligible)
                {
                    throw new ConflictException("correction_grant_ineligible", "Linked transaction is not eligible for correction support.");
                }
            }
        }

        var owner = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == command.UserId, cancellationToken)
            ?? throw new NotFoundException("user_not_found", "User not found.");
        var supportCase = new SupportCase
        {
            TicketNumber = $"SW-{clock.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            LinkedUserId = command.UserId,
            LinkedTransactionId = command.Request.LinkedTransactionId,
            Category = category,
            Priority = priority,
            Subject = subject,
            Description = description,
            Status = CaseStatus.New
        };
        return await db.ExecuteInTransactionAsync(async token =>
        {
            // Case, history và email biên nhận cùng commit hoặc cùng rollback.
            db.SupportCases.Add(supportCase);
            db.CaseHistories.Add(new CaseHistory
            {
                CaseKind = CaseKind.Support,
                CaseId = supportCase.Id,
                ActorId = command.UserId,
                ActorDisplayNameSnapshot = owner.FullName,
                ActorRole = "User",
                Action = "create",
                Message = description,
                NewStatus = "pending"
            });
            await db.SaveChangesAsync(token);
            await supportEmails.QueueReceivedAsync(owner, supportCase, token);

            return AdminMappings.ToSupportCaseDto(supportCase);
        }, cancellationToken);
    }
}
