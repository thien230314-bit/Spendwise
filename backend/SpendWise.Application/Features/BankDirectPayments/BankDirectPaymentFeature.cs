using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features;
using SpendWise.Application.Finance;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Groups;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.BankDirectPayments;

public sealed record FindBankTransferRecipientQuery(Guid UserId, string Phone) : IRequest<BankTransferRecipientDto>;
public sealed record CreateBankDirectTransferCommand(Guid UserId, string? SessionId, CreateBankDirectTransferRequest Request) : IRequest<BankDirectPaymentDto>;
public sealed record ConfirmBankDirectPaymentCommand(Guid UserId, Guid PaymentId, ConfirmBankDirectPaymentRequest Request) : IRequest<BankDirectPaymentDto>;
public sealed record GetBankDirectPaymentQuery(Guid UserId, Guid PaymentId) : IRequest<BankDirectPaymentDto>;
public sealed record GetBankDirectPaymentHistoryQuery(Guid UserId, BankDirectPaymentHistoryFilter Filter) : IRequest<PagedResult<BankDirectPaymentDto>>;

public sealed record BankDirectPaymentDraft(
    Guid SenderUserId,
    User? Recipient,
    BankConnection RecipientBank,
    decimal Amount,
    string? Note,
    string ClientRequestKey,
    BankDirectPaymentContextType ContextType,
    Guid? GroupId = null,
    Guid? FundRequestId = null,
    Guid? FundRequestRecipientId = null,
    Guid? FundRoundId = null,
    Guid? FundRoundMemberId = null,
    Guid? DeletionRequestId = null,
    Guid? GroupRefundId = null,
    string? RecipientDisplayName = null,
    string? RecipientPhone = null);

public sealed class BankDirectPaymentSupport(ISpendWiseDbContext db)
{
    public async Task<BankConnection> ActiveBankForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.BankConnections
            .Where(item => item.UserId == userId && item.Status == BankConnectionStatus.Active)
            .OrderByDescending(item => item.LinkedAt)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new ConflictException("active_bank_required", "Recipient must have an active bank connection.");

    public async Task<GroupReceivingBank> ReceivingBankAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var receiving = await db.GroupReceivingBanks.AsNoTracking()
            .SingleOrDefaultAsync(item => item.GroupId == groupId, cancellationToken)
            ?? throw new ConflictException("group_receiving_bank_required", "The group receiving bank is not configured.");
        var active = await db.BankConnections.AsNoTracking().AnyAsync(item => item.Id == receiving.BankConnectionId
            && item.UserId == receiving.OwnerUserId && item.Status == BankConnectionStatus.Active, cancellationToken);
        if (!active)
        {
            throw new ConflictException("group_receiving_bank_inactive", "The configured group receiving bank is inactive.");
        }
        return receiving;
    }

    public async Task<BankDirectPaymentDto> CreateAsync(BankDirectPaymentDraft draft, CancellationToken cancellationToken)
    {
        InputBoundary.PositiveMoney(draft.Amount);
        var key = InputBoundary.RequiredText(draft.ClientRequestKey, 100, "Idempotency key");
        var note = InputBoundary.OptionalText(draft.Note, 500, "Note");
        var existing = await db.BankDirectPayments.AsNoTracking().SingleOrDefaultAsync(
            item => item.SenderUserId == draft.SenderUserId
                && item.ContextType == draft.ContextType
                && item.ClientRequestKey == key,
            cancellationToken);
        if (existing is not null)
        {
            EnsureSameRequest(existing, draft, note);
            return Map(existing);
        }

        if (draft.RecipientBank.Status != BankConnectionStatus.Active)
        {
            throw new ConflictException("active_bank_required", "Recipient bank connection is inactive.");
        }

        var payment = new BankDirectPayment
        {
            SenderUserId = draft.SenderUserId,
            RecipientUserId = draft.Recipient?.Id,
            RecipientDisplayNameSnapshot = draft.RecipientDisplayName ?? draft.Recipient?.FullName ?? draft.RecipientBank.AccountHolderName ?? "Recipient",
            RecipientPhoneSnapshot = draft.RecipientPhone ?? draft.Recipient?.Phone,
            RecipientBankConnectionId = draft.RecipientBank.Id,
            BankBrandSnapshot = draft.RecipientBank.BankBrandName,
            AccountHolderSnapshot = draft.RecipientBank.AccountHolderName ?? draft.Recipient?.FullName ?? "Account holder",
            MaskedAccountSnapshot = draft.RecipientBank.MaskedAccountNumber,
            Amount = draft.Amount,
            Note = note,
            PaymentReference = Reference(draft.ContextType),
            ClientRequestKey = key,
            ContextType = draft.ContextType,
            GroupId = draft.GroupId,
            FundRequestId = draft.FundRequestId,
            FundRequestRecipientId = draft.FundRequestRecipientId,
            FundRoundId = draft.FundRoundId,
            FundRoundMemberId = draft.FundRoundMemberId,
            DeletionRequestId = draft.DeletionRequestId,
            GroupRefundId = draft.GroupRefundId
        };
        db.BankDirectPayments.Add(payment);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index là hàng rào cuối khi hai request cùng key đến đồng thời.
            db.ClearTrackedEntities();
            var replay = await db.BankDirectPayments.AsNoTracking().SingleOrDefaultAsync(
                item => item.SenderUserId == draft.SenderUserId
                    && item.ContextType == draft.ContextType
                    && item.ClientRequestKey == key,
                cancellationToken);
            if (replay is null)
            {
                throw;
            }
            EnsureSameRequest(replay, draft, note);
            return Map(replay);
        }
        return Map(payment);
    }

    private static void EnsureSameRequest(BankDirectPayment existing, BankDirectPaymentDraft draft, string? note)
    {
        if (existing.RecipientUserId != draft.Recipient?.Id
            || existing.RecipientBankConnectionId != draft.RecipientBank.Id
            || existing.Amount != draft.Amount
            || existing.Note != note
            || existing.GroupId != draft.GroupId
            || existing.FundRequestRecipientId != draft.FundRequestRecipientId
            || existing.FundRoundMemberId != draft.FundRoundMemberId
            || existing.GroupRefundId != draft.GroupRefundId)
        {
            throw new ConflictException("idempotency_conflict", "The idempotency key was used with different payment details.");
        }
    }

    public static BankDirectPaymentDto Map(BankDirectPayment payment) => new(
        payment.Id,
        payment.SenderUserId,
        payment.RecipientUserId,
        payment.RecipientDisplayNameSnapshot,
        payment.RecipientPhoneSnapshot,
        payment.RecipientBankConnectionId,
        payment.BankBrandSnapshot,
        payment.AccountHolderSnapshot,
        payment.MaskedAccountSnapshot,
        payment.Amount,
        payment.Currency,
        payment.Note,
        payment.PaymentReference,
        payment.ContextType.ToString(),
        payment.GroupId,
        payment.Status.ToString(),
        payment.CreatedAt,
        payment.SenderConfirmedAt,
        payment.CompletedAt,
        payment.MatchedBankTransactionId);

    private static string Reference(BankDirectPaymentContextType context) =>
        $"SW-{context switch
        {
            BankDirectPaymentContextType.Transfer => "TRF",
            BankDirectPaymentContextType.GroupContribution => "GCT",
            BankDirectPaymentContextType.GroupFundRequest => "GFR",
            BankDirectPaymentContextType.GroupFundRound => "GRD",
            BankDirectPaymentContextType.GroupRefund => "RFD",
            _ => "PAY"
        }}-{Guid.NewGuid():N}".ToUpperInvariant();
}

public sealed class FindBankTransferRecipientQueryHandler(ISpendWiseDbContext db, BankDirectPaymentSupport payments)
    : IRequestHandler<FindBankTransferRecipientQuery, BankTransferRecipientDto>
{
    public async Task<BankTransferRecipientDto> Handle(FindBankTransferRecipientQuery query, CancellationToken cancellationToken)
    {
        var phone = InputBoundary.NormalizePhone(query.Phone);
        var recipient = await db.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Phone == phone
            && item.SystemRole == SystemRole.User && item.Status == UserStatus.Active, cancellationToken)
            ?? throw new NotFoundException("recipient_not_found", "Eligible recipient not found.");
        if (recipient.Id == query.UserId)
        {
            throw new AppException("self_transfer", "Bank transfer to yourself is not allowed.");
        }
        var bank = await payments.ActiveBankForUserAsync(recipient.Id, cancellationToken);
        return new BankTransferRecipientDto(recipient.Id, recipient.FullName, bank.BankBrandName,
            bank.AccountHolderName ?? recipient.FullName, bank.MaskedAccountNumber);
    }
}

public sealed class CreateBankDirectTransferCommandHandler(
    ISpendWiseDbContext db,
    BankDirectPaymentSupport payments,
    IRiskService risk,
    IOperationPolicy policy)
    : IRequestHandler<CreateBankDirectTransferCommand, BankDirectPaymentDto>
{
    public async Task<BankDirectPaymentDto> Handle(CreateBankDirectTransferCommand command, CancellationToken cancellationToken)
    {
        await policy.EnsureFeatureEnabledAsync("P2PTransfer", cancellationToken);
        InputBoundary.PositiveMoney(command.Request.Amount);
        if (command.UserId == command.Request.RecipientUserId)
        {
            throw new AppException("self_transfer", "Bank transfer to yourself is not allowed.");
        }
        var minimum = await policy.GetDecimalAsync("TransferMinimum", 1_000m, cancellationToken);
        if (command.Request.Amount < minimum)
        {
            throw new AppException("transfer_below_minimum", $"Transfer amount must be at least {minimum:N0} VND.");
        }
        var assessment = await risk.AssessTransferAsync(command.UserId, command.Request.Amount, command.SessionId, cancellationToken);
        if (assessment.Decision is "Block" or "Review")
        {
            throw new AppException("transfer_risk_blocked", $"Transfer requires risk review ({assessment.RiskCaseId}).", 403);
        }
        var recipient = await db.Users.SingleOrDefaultAsync(item => item.Id == command.Request.RecipientUserId
            && item.SystemRole == SystemRole.User && item.Status == UserStatus.Active, cancellationToken)
            ?? throw new NotFoundException("recipient_not_found", "Eligible recipient not found.");
        var bank = await payments.ActiveBankForUserAsync(recipient.Id, cancellationToken);
        return await payments.CreateAsync(new BankDirectPaymentDraft(command.UserId, recipient, bank,
            command.Request.Amount, command.Request.Note, command.Request.IdempotencyKey,
            BankDirectPaymentContextType.Transfer), cancellationToken);
    }
}

public sealed class ConfirmBankDirectPaymentCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    FinancialSecurityGuard security,
    IBankDirectPaymentReconciler reconciler)
    : IRequestHandler<ConfirmBankDirectPaymentCommand, BankDirectPaymentDto>
{
    public async Task<BankDirectPaymentDto> Handle(ConfirmBankDirectPaymentCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        var payment = await db.BankDirectPayments.SingleOrDefaultAsync(item => item.Id == command.PaymentId
            && item.SenderUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("bank_payment_not_found", "Bank-direct payment not found.");
        if (payment.Status == BankDirectPaymentStatus.Completed)
        {
            return BankDirectPaymentSupport.Map(payment);
        }
        if (payment.Status == BankDirectPaymentStatus.Pending)
        {
            payment.Status = BankDirectPaymentStatus.AwaitingReconciliation;
            payment.SenderConfirmedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (payment.Status != BankDirectPaymentStatus.AwaitingReconciliation)
        {
            throw new ConflictException("bank_payment_unavailable", "Payment cannot be confirmed in its current status.");
        }

        // Nếu IPN đến trước thao tác xác nhận, dữ liệu ngân hàng tin cậy vẫn có thể hoàn tất payment sau xác nhận.
        var candidates = await db.BankTransactions.AsNoTracking().Where(item => item.BankConnectionId == payment.RecipientBankConnectionId
            && item.Direction == BankTransactionDirection.Credit && item.Amount == payment.Amount
            && item.Currency == payment.Currency).OrderByDescending(item => item.TransactionDate).Take(20).ToListAsync(cancellationToken);
        foreach (var candidate in candidates)
        {
            if (await reconciler.ReconcileAsync(candidate, cancellationToken))
            {
                break;
            }
        }
        var stored = await db.BankDirectPayments.AsNoTracking().SingleAsync(item => item.Id == payment.Id, cancellationToken);
        return BankDirectPaymentSupport.Map(stored);
    }
}

public sealed class GetBankDirectPaymentQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetBankDirectPaymentQuery, BankDirectPaymentDto>
{
    public async Task<BankDirectPaymentDto> Handle(GetBankDirectPaymentQuery query, CancellationToken cancellationToken)
    {
        var payment = await db.BankDirectPayments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == query.PaymentId
            && (item.SenderUserId == query.UserId || item.RecipientUserId == query.UserId), cancellationToken)
            ?? throw new NotFoundException("bank_payment_not_found", "Bank-direct payment not found.");
        return BankDirectPaymentSupport.Map(payment);
    }
}

public sealed class GetBankDirectPaymentHistoryQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetBankDirectPaymentHistoryQuery, PagedResult<BankDirectPaymentDto>>
{
    public async Task<PagedResult<BankDirectPaymentDto>> Handle(GetBankDirectPaymentHistoryQuery query, CancellationToken cancellationToken)
    {
        InputBoundary.Page(query.Filter.Page, query.Filter.PageSize);
        var source = db.BankDirectPayments.AsNoTracking().Where(item => item.SenderUserId == query.UserId
            || item.RecipientUserId == query.UserId);
        var total = await source.CountAsync(cancellationToken);
        var items = await source.OrderByDescending(item => item.CreatedAt)
            .Skip((query.Filter.Page - 1) * query.Filter.PageSize).Take(query.Filter.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<BankDirectPaymentDto>(items.Select(BankDirectPaymentSupport.Map).ToList(),
            query.Filter.Page, query.Filter.PageSize, total);
    }
}

public sealed class BankDirectPaymentReconciler(
    ISpendWiseDbContext db,
    IClock clock,
    INotificationService notifications) : IBankDirectPaymentReconciler
{
    public async Task<bool> ReconcileAsync(BankTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.Direction != BankTransactionDirection.Credit)
        {
            return false;
        }
        var candidates = await db.BankDirectPayments.AsNoTracking().Where(item => item.Status == BankDirectPaymentStatus.AwaitingReconciliation
            && item.ContextType == BankDirectPaymentContextType.Transfer
            && item.RecipientBankConnectionId == transaction.BankConnectionId
            && item.Amount == transaction.Amount
            && item.Currency == transaction.Currency).ToListAsync(cancellationToken);
        var matches = candidates.Where(item => ContainsReference(transaction, item.PaymentReference)).ToList();
        if (matches.Count != 1)
        {
            return false;
        }

        var paymentId = matches[0].Id;
        var completedAt = clock.UtcNow;
        return await db.ExecuteInTransactionAsync(async ct =>
        {
            // Chỉ một webhook được quyền chuyển trạng thái khỏi AwaitingReconciliation.
            var claimed = await db.BankDirectPayments
                .Where(item => item.Id == paymentId
                    && item.Status == BankDirectPaymentStatus.AwaitingReconciliation
                    && item.MatchedBankTransactionId == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, BankDirectPaymentStatus.Completed)
                    .SetProperty(item => item.MatchedBankTransactionId, transaction.Id)
                    .SetProperty(item => item.CompletedAt, completedAt), ct);
            if (claimed != 1)
            {
                return false;
            }

            // ExecuteUpdate không đồng bộ ChangeTracker nên nạp lại payment đã được claim.
            db.ClearTrackedEntities();
            var payment = await db.BankDirectPayments.SingleAsync(item => item.Id == paymentId, ct);
            await ApplyContextCompletionAsync(payment, ct);
            await db.SaveChangesAsync(ct);

            await notifications.CreateAsync(payment.SenderUserId, "BankDirectPaymentCompleted", "Bank payment completed",
                $"Payment {payment.PaymentReference} was reconciled.", new { payment.Id, payment.ContextType }, ct);
            if (payment.RecipientUserId is Guid recipientId && recipientId != payment.SenderUserId)
            {
                await notifications.CreateAsync(recipientId, "BankDirectPaymentCompleted", "Bank payment received",
                    $"Payment {payment.PaymentReference} was reconciled.", new { payment.Id, payment.ContextType }, ct);
            }
            return true;
        }, cancellationToken);
    }

    private async Task ApplyContextCompletionAsync(BankDirectPayment payment, CancellationToken cancellationToken)
    {
        if (payment.ContextType == BankDirectPaymentContextType.Transfer)
        {
            return;
        }
        if (payment.ContextType == BankDirectPaymentContextType.GroupRefund)
        {
            await CompleteRefundAsync(payment, cancellationToken);
            return;
        }

        var groupId = payment.GroupId ?? throw new InvalidOperationException("Group payment has no group id.");
        var fund = await db.SharedFunds.SingleAsync(item => item.GroupId == groupId, cancellationToken);
        fund.Balance += payment.Amount;
        var transaction = new FundTransaction
        {
            GroupId = groupId,
            Type = FundTransactionType.Contribution,
            Amount = payment.Amount,
            UserId = payment.SenderUserId,
            UserNameSnapshot = await db.Users.Where(item => item.Id == payment.SenderUserId).Select(item => item.FullName).SingleAsync(cancellationToken),
            Description = payment.Note ?? "Group fund contribution",
            Date = clock.UtcNow,
            ReferenceCode = payment.PaymentReference,
            PaymentId = payment.Id
        };
        db.FundTransactions.Add(transaction);
        db.GroupActivities.Add(new GroupActivity
        {
            GroupId = groupId,
            ActorUserId = payment.SenderUserId,
            Type = payment.ContextType == BankDirectPaymentContextType.GroupFundRound
                ? GroupActivityType.FundRoundPaid
                : GroupActivityType.FundContribution,
            DataJson = JsonSerializer.Serialize(new { payment.Id, payment.Amount, payment.PaymentReference })
        });

        if (payment.ContextType == BankDirectPaymentContextType.GroupFundRequest)
        {
            var recipient = await db.FundRequestRecipients.SingleAsync(item => item.Id == payment.FundRequestRecipientId, cancellationToken);
            recipient.Status = FundRequestRecipientStatus.Paid;
            recipient.PaymentId = payment.Id;
            recipient.PaidAt = clock.UtcNow;
        }
        else if (payment.ContextType == BankDirectPaymentContextType.GroupFundRound)
        {
            var member = await db.GroupFundRoundMembers.SingleAsync(item => item.Id == payment.FundRoundMemberId, cancellationToken);
            var round = await db.GroupFundRounds.Include(item => item.Members).SingleAsync(item => item.Id == payment.FundRoundId, cancellationToken);
            member.PaidAmount += payment.Amount;
            member.Status = member.PaidAmount >= member.RequiredAmount
                ? GroupFundRoundMemberStatus.Paid
                : GroupFundRoundMemberStatus.Partial;
            round.CollectedAmount += payment.Amount;
            if (round.Members.All(item => item.Id == member.Id
                    ? member.Status == GroupFundRoundMemberStatus.Paid
                    : item.Status == GroupFundRoundMemberStatus.Paid))
            {
                round.Status = GroupFundRoundStatus.Completed;
                round.CompletedAt = clock.UtcNow;
                db.GroupActivities.Add(new GroupActivity
                {
                    GroupId = groupId,
                    ActorUserId = payment.SenderUserId,
                    Type = GroupActivityType.FundRoundCompleted,
                    DataJson = JsonSerializer.Serialize(new { round.Id, round.TargetAmount })
                });
            }
        }
    }

    private async Task CompleteRefundAsync(BankDirectPayment payment, CancellationToken cancellationToken)
    {
        var refund = await db.GroupRefunds.SingleAsync(item => item.Id == payment.GroupRefundId, cancellationToken);
        if (refund.Status == GroupRefundStatus.Completed)
        {
            return;
        }
        var request = await db.GroupDeletionRequests.Include(item => item.Refunds)
            .SingleAsync(item => item.Id == refund.GroupDeletionRequestId, cancellationToken);
        var fund = await db.SharedFunds.SingleAsync(item => item.GroupId == request.GroupId, cancellationToken);
        if (fund.Balance < refund.RefundAmount)
        {
            throw new ConflictException("group_refund_balance_changed", "Group fund no longer covers the refund plan.");
        }
        refund.Status = GroupRefundStatus.Completed;
        refund.CompletedAt = clock.UtcNow;
        fund.Balance -= refund.RefundAmount;
        await db.SaveChangesAsync(cancellationToken);

        var allCompleted = await db.GroupRefunds.Where(item => item.GroupDeletionRequestId == request.Id)
            .AllAsync(item => item.RefundAmount == 0 || item.Status == GroupRefundStatus.Completed, cancellationToken);
        // Đối soát chỉ hoàn tất refund; Owner phải finalize bằng PIN ở endpoint riêng.
        if (allCompleted)
            request.Status = GroupDeletionRequestStatus.Ready;
        await notifications.CreateAsync(refund.UserId, "GroupRefundCompleted", "Group refund completed",
            $"Your refund of {refund.RefundAmount:N0} VND was reconciled.",
            new { refundId = refund.Id, paymentId = payment.Id }, cancellationToken);
    }

    private static bool ContainsReference(BankTransaction transaction, string reference) =>
        new[] { transaction.Content, transaction.ReferenceCode, transaction.PaymentCode }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Any(value => value!.Contains(reference, StringComparison.OrdinalIgnoreCase));
}
