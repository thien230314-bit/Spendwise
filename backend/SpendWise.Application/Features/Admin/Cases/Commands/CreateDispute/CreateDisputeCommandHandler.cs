using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.Admin.Cases.Commands.CreateDispute;

// Handler mở tranh chấp cho giao dịch theo workflow kiểm soát.

public sealed class CreateDisputeCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CreateDisputeCommand, CaseDto>
{
    public async Task<CaseDto> Handle(
        CreateDisputeCommand command,
        CancellationToken cancellationToken)
    {
        // Xác nhận giao dịch thuộc người dùng trước khi mở hồ sơ tranh chấp.
        var reason = InputBoundary.RequiredText(command.Request.Reason, 100, "Dispute reason").ToLowerInvariant();
        var allowedReasons = new[] { "unrecognized_charge", "duplicate_transaction", "duplicate_deduction", "incorrect_amount", "goods_not_received", "merchant_dispute", "other" };
        if (!allowedReasons.Contains(reason)) throw new AppException("invalid_dispute_reason", "Dispute reason is invalid.");
        var description = InputBoundary.RequiredText(command.Request.Description, 4000, "Description");
        var transaction = await db.FinancialTransactions.Include(x => x.LedgerTransaction).SingleOrDefaultAsync(
            transaction => transaction.Id == command.Request.LinkedTransactionId
                && transaction.OwnerUserId == command.UserId,
            cancellationToken);
        if (transaction is null)
        {
            throw new NotFoundException(
                "transaction_not_found",
                "Transaction not found.");
        }

        var dispute = new Dispute
        {
            UserId = command.UserId,
            LinkedTransactionId = command.Request.LinkedTransactionId,
            Reason = reason,
            Description = description,
            TransactionReferenceSnapshot = transaction.LedgerTransaction.Reference,
            TransactionTitleSnapshot = transaction.Description,
            TransactionAmountSnapshot = transaction.Amount,
            CounterpartySnapshot = transaction.CounterpartyName,
            TransactionOccurredAtSnapshot = transaction.OccurredAt,
            Status = CaseStatus.New
        };
        db.Disputes.Add(dispute);
        var actorName = await db.Users.Where(x => x.Id == command.UserId).Select(x => x.FullName).SingleAsync(cancellationToken);
        db.CaseHistories.Add(new CaseHistory { CaseKind = CaseKind.Dispute, CaseId = dispute.Id,
            ActorId = command.UserId, ActorDisplayNameSnapshot = actorName, ActorRole = "User",
            Action = "create", Message = description, NewStatus = "pending" });
        await db.SaveChangesAsync(cancellationToken);

        return AdminMappings.ToDisputeDto(dispute);
    }
}
