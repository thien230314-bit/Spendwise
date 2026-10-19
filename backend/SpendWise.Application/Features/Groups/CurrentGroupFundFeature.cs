using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.BankDirectPayments;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Application.Notifications;
using SpendWise.Domain.Banking;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.Groups;

public sealed record SetGroupReceivingBankCommand(Guid UserId, Guid GroupId, GroupReceivingBankRequest Request) : IRequest<GroupReceivingBankDto>;
public sealed record GetGroupInvitationsQuery(Guid UserId) : IRequest<IReadOnlyList<GroupInviteDto>>;
public sealed record RejectGroupInviteCommand(Guid UserId, Guid InviteId) : IRequest<GroupInviteDto>;
public sealed record CreateFundContributionPaymentCommand(Guid UserId, Guid GroupId, CreateFundContributionPaymentRequest Request) : IRequest<GroupContributionDto>;
public sealed record GetFundTransactionsQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<FundTransactionDto>>;
public sealed record CreateFundRequestCommand(Guid UserId, Guid GroupId, CreateFundRequestRequest Request) : IRequest<FundRequestDto>;
public sealed record GetFundRequestsQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<FundRequestDto>>;
public sealed record PayFundRequestCommand(Guid UserId, Guid GroupId, Guid FundRequestId, PayFundRequestRequest Request) : IRequest<GroupContributionDto>;
public sealed record CreateFundRoundCommand(Guid UserId, Guid GroupId, CreateFundRoundRequest Request) : IRequest<FundRoundDto>;
public sealed record GetFundRoundsQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<FundRoundDto>>;
public sealed record PayFundRoundCommand(Guid UserId, Guid GroupId, Guid RoundId, PayFundRoundRequest Request) : IRequest<GroupContributionDto>;
public sealed record CreateCurrentGroupExpenseCommand(Guid UserId, Guid GroupId, CreateGroupExpenseRequest Request) : IRequest<GroupExpenseDto>;
public sealed record GetCurrentGroupExpensesQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<GroupExpenseDto>>;
public sealed record GetGroupActivitiesQuery(Guid UserId, Guid GroupId) : IRequest<IReadOnlyList<GroupActivityDto>>;
public sealed record CreateGroupDeletionRequestCommand(Guid UserId, Guid GroupId) : IRequest<GroupDeletionRequestDto>;
public sealed record GetGroupDeletionRequestQuery(Guid UserId, Guid GroupId) : IRequest<GroupDeletionRequestDto?>;
public sealed record VoteGroupDeletionCommand(Guid UserId, Guid GroupId, Guid RequestId, GroupDeletionVoteRequest Request) : IRequest<GroupDeletionRequestDto>;
public sealed record CancelGroupDeletionCommand(Guid UserId, Guid GroupId, Guid RequestId) : IRequest<GroupDeletionRequestDto>;
public sealed record CreateGroupRefundPaymentCommand(Guid UserId, Guid GroupId, Guid RequestId, Guid RefundId, CreateGroupRefundPaymentRequest Request) : IRequest<GroupRefundDto>;
public sealed record FinalizeGroupDeletionCommand(Guid UserId, Guid GroupId, Guid RequestId, FinalizeGroupDeletionRequest Request) : IRequest<GroupDeletionRequestDto>;

internal static class CurrentGroupMappings
{
    public static GroupRefundDto Refund(GroupRefund item) => new(item.Id, item.UserId, item.UserNameSnapshot,
        item.ContributionBasis, item.RefundAmount, item.Percentage, item.Status.ToString(), item.PaymentId,
        item.PaymentReference, item.CompletedAt, item.DestinationWalletId, item.LedgerTransactionId);
    public static GroupReceivingBankDto Receiving(GroupReceivingBank item) => new(item.Id, item.OwnerUserId,
        item.BankConnectionId, item.BankBrandSnapshot, item.MaskedAccountSnapshot, item.AccountHolderSnapshot);

    public static FundRequestDto FundRequest(FundRequest item) => new(item.Id, item.GroupId, item.Purpose,
        item.AmountPerMember, item.DueDate,
        item.Recipients.OrderBy(recipient => recipient.UserNameSnapshot).Select(recipient =>
            new FundRequestRecipientDto(recipient.Id, recipient.UserId, recipient.UserNameSnapshot,
                recipient.Status.ToString(), recipient.PaymentId, recipient.PaidAt)).ToList(), item.CreatedAt);

    public static FundRoundDto Round(GroupFundRound item) => new(item.Id, item.GroupId, item.Title,
        item.Description, item.TargetAmount, item.CollectedAmount, item.Status.ToString(), item.CreatedAt,
        item.CompletedAt, item.Members.OrderBy(member => member.UserNameSnapshot).Select(member =>
            new FundRoundMemberDto(member.Id, member.UserId, member.UserNameSnapshot, member.RequiredAmount,
                member.PaidAmount, member.Status.ToString())).ToList());

    public static GroupExpenseDto Expense(GroupExpense item) => new(item.Id, item.GroupId, item.Title,
        item.Amount, item.CategoryId, item.CategorySnapshot, item.PaidByType.ToString(), item.PaidByUserId,
        item.Date, item.Description, item.Status.ToString());

    public static GroupDeletionRequestDto Deletion(GroupDeletionRequest item) => new(item.Id, item.GroupId,
        item.Status.ToString(), item.FundBalanceSnapshot, item.CreatedAt, item.ReadyAt, item.CompletedAt,
        item.Votes.OrderBy(vote => vote.UserNameSnapshot).Select(vote => new GroupDeletionVoteDto(vote.Id,
            vote.UserId, vote.UserNameSnapshot, vote.Status.ToString(), vote.VotedAt)).ToList(),
        item.Refunds.OrderBy(refund => refund.UserNameSnapshot).Select(Refund).ToList());
}

public sealed class GroupDeletionSupport(
    ISpendWiseDbContext db,
    IClock clock,
    INotificationService notifications,
    WalletFundAccounting accounting)
{
    public async Task MakeReadyAsync(GroupDeletionRequest request, CancellationToken cancellationToken)
    {
        if (request.Status == GroupDeletionRequestStatus.Ready || request.Status == GroupDeletionRequestStatus.Completed)
        {
            return;
        }
        var fund = await accounting.LoadCompatibleFundAsync(request.GroupId, cancellationToken);
        request.Status = GroupDeletionRequestStatus.Ready;
        request.ReadyAt = clock.UtcNow;
        request.FundBalanceSnapshot = fund.Balance;

        var votes = request.Votes.Count == 0
            ? await db.GroupDeletionVotes.Where(item => item.GroupDeletionRequestId == request.Id).ToListAsync(cancellationToken)
            : request.Votes;
        var userIds = votes.Select(item => item.UserId).ToList();
        var contributionBasis = await db.GroupFundShares.AsNoTracking()
            .Where(item => item.GroupId == request.GroupId && userIds.Contains(item.UserId))
            .Select(item => new { item.UserId, Amount = item.RemainingAmount })
            .ToDictionaryAsync(item => item.UserId, item => item.Amount, cancellationToken);
        var ordered = votes.OrderBy(item => item.UserId).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var vote = ordered[index];
            var basis = contributionBasis.GetValueOrDefault(vote.UserId);
            var amount = basis;
            var refund = new GroupRefund
            {
                GroupDeletionRequestId = request.Id,
                UserId = vote.UserId,
                UserNameSnapshot = vote.UserNameSnapshot,
                ContributionBasis = basis,
                RefundAmount = amount,
                Percentage = fund.Balance == 0 ? 0 : decimal.Round(amount / fund.Balance * 100, 6),
                Status = amount == 0 ? GroupRefundStatus.Completed : GroupRefundStatus.Pending,
                CompletedAt = amount == 0 ? clock.UtcNow : null
            };
            request.Refunds.Add(refund);
            // Id được tạo ở ứng dụng nên phải đánh dấu rõ đây là bản ghi mới, không phải bản ghi cần UPDATE.
            db.GroupRefunds.Add(refund);
        }

        var group = await db.Groups.SingleAsync(item => item.Id == request.GroupId, cancellationToken);
        // Kể cả quỹ bằng 0, Group vẫn chờ Owner finalize bằng PIN ở bước riêng.
        group.Status = GroupStatus.DeletionPending;
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateManyAsync(votes.Select(vote => new CreateNotificationRequest(vote.UserId,
            "GroupDeletionReady", "Group deletion ready", "The refund plan is ready.",
            new { request.Id, request.GroupId })).ToList(), cancellationToken);
    }
}

public sealed class SetGroupReceivingBankCommandHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<SetGroupReceivingBankCommand, GroupReceivingBankDto>
{
    public async Task<GroupReceivingBankDto> Handle(SetGroupReceivingBankCommand command, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(command.UserId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var connection = await db.BankConnections.SingleOrDefaultAsync(item => item.Id == command.Request.BankConnectionId
            && item.UserId == command.UserId && item.Status == BankConnectionStatus.Active, cancellationToken)
            ?? throw new NotFoundException("active_bank_not_found", "Selected active bank connection was not found.");
        var item = await db.GroupReceivingBanks.SingleOrDefaultAsync(bank => bank.GroupId == command.GroupId, cancellationToken);
        if (item is null)
        {
            item = new GroupReceivingBank { GroupId = command.GroupId };
            db.GroupReceivingBanks.Add(item);
        }
        item.OwnerUserId = command.UserId;
        item.BankConnectionId = connection.Id;
        item.BankBrandSnapshot = connection.BankBrandName;
        item.MaskedAccountSnapshot = connection.MaskedAccountNumber;
        item.AccountHolderSnapshot = connection.AccountHolderName ?? "Account holder";
        await db.SaveChangesAsync(cancellationToken);
        return CurrentGroupMappings.Receiving(item);
    }
}

public sealed class GetGroupInvitationsQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetGroupInvitationsQuery, IReadOnlyList<GroupInviteDto>>
{
    public async Task<IReadOnlyList<GroupInviteDto>> Handle(GetGroupInvitationsQuery query, CancellationToken cancellationToken) =>
        await db.GroupInvites.AsNoTracking().Where(item => item.InvitedUserId == query.UserId)
            .OrderByDescending(item => item.CreatedAt)
            .Join(db.Groups.AsNoTracking(), invite => invite.GroupId, group => group.Id,
                (invite, group) => new GroupInviteDto(invite.Id, group.Id, group.Name, invite.InvitedUserId,
                    invite.Status.ToString(), invite.ExpiresAt, invite.Token))
            .ToListAsync(cancellationToken);
}

public sealed class RejectGroupInviteCommandHandler(ISpendWiseDbContext db)
    : IRequestHandler<RejectGroupInviteCommand, GroupInviteDto>
{
    public async Task<GroupInviteDto> Handle(RejectGroupInviteCommand command, CancellationToken cancellationToken)
    {
        var invite = await db.GroupInvites.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.InviteId
            && item.InvitedUserId == command.UserId, cancellationToken)
            ?? throw new NotFoundException("invite_not_found", "Invite not found.");
        if (invite.Status == InviteStatus.Pending)
        {
            if (!await GroupAdmission.ResolveInviteAsync(db, invite.Id, InviteStatus.Rejected, cancellationToken)
                && !await db.GroupInvites.AsNoTracking().AnyAsync(i => i.Id == invite.Id && i.Status == InviteStatus.Rejected, cancellationToken))
                throw new ConflictException("invite_unavailable", "Invite is no longer pending.");
            invite.Status = InviteStatus.Rejected;
        }
        else if (invite.Status != InviteStatus.Rejected)
        {
            throw new ConflictException("invite_unavailable", "Invite cannot be rejected.");
        }
        var groupName = await db.Groups.Where(item => item.Id == invite.GroupId).Select(item => item.Name).SingleAsync(cancellationToken);
        return new GroupInviteDto(invite.Id, invite.GroupId, groupName, invite.InvitedUserId,
            invite.Status.ToString(), invite.ExpiresAt, invite.Token);
    }
}

public sealed class CreateFundContributionPaymentCommandHandler(WalletFundAccounting accounting)
    : IRequestHandler<CreateFundContributionPaymentCommand, GroupContributionDto>
{
    public Task<GroupContributionDto> Handle(CreateFundContributionPaymentCommand command, CancellationToken cancellationToken) =>
        accounting.ContributeAsync(command.UserId, command.GroupId, command.Request.WalletId,
            command.Request.Amount, command.Request.Purpose, command.Request.IdempotencyKey,
            command.Request.SecurityPin, null, cancellationToken);
}

public sealed class GetFundTransactionsQueryHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<GetFundTransactionsQuery, IReadOnlyList<FundTransactionDto>>
{
    public async Task<IReadOnlyList<FundTransactionDto>> Handle(GetFundTransactionsQuery query, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        return await db.FundTransactions.AsNoTracking().Where(item => item.GroupId == query.GroupId)
            .OrderByDescending(item => item.Date).Select(item => new FundTransactionDto(item.Id,
                item.Type.ToString(), item.Amount, item.UserId, item.UserNameSnapshot, item.Description,
                item.Date, item.ReferenceCode, item.PaymentId, item.WalletId, item.LedgerTransactionId)).ToListAsync(cancellationToken);
    }
}

public sealed class CreateFundRequestCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    IClock clock,
    INotificationService notifications)
    : IRequestHandler<CreateFundRequestCommand, FundRequestDto>
{
    public Task<FundRequestDto> Handle(CreateFundRequestCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(token => CreateAsync(command, token), cancellationToken);

    private async Task<FundRequestDto> CreateAsync(CreateFundRequestCommand command, CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        await access.RequireOwnerAsync(command.UserId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var purpose = InputBoundary.RequiredText(command.Request.Purpose, 500, "Purpose");
        InputBoundary.PositiveMoney(command.Request.AmountPerMember);
        InputBoundary.FutureDate(command.Request.DueDate, clock.UtcNow, "Due date");
        var ids = command.Request.RecipientUserIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new AppException("fund_request_recipients_required", "Select at least one active group member.");
        }
        var recipients = await db.GroupMembers.AsNoTracking().Where(item => item.GroupId == command.GroupId
            && ids.Contains(item.UserId)).Select(item => new { item.UserId, item.User.FullName }).ToListAsync(cancellationToken);
        if (recipients.Count != ids.Count)
        {
            throw new AppException("invalid_fund_request_recipient", "Every recipient must be an active group member.");
        }
        var item = new FundRequest
        {
            GroupId = command.GroupId,
            CreatedByUserId = command.UserId,
            Purpose = purpose,
            AmountPerMember = command.Request.AmountPerMember,
            DueDate = command.Request.DueDate?.ToUniversalTime(),
            Recipients = recipients.Select(recipient => new FundRequestRecipient
            {
                UserId = recipient.UserId,
                UserNameSnapshot = recipient.FullName
            }).ToList()
        };
        db.FundRequests.Add(item);
        db.GroupActivities.Add(new GroupActivity { GroupId = command.GroupId, ActorUserId = command.UserId,
            Type = GroupActivityType.Request, DataJson = JsonSerializer.Serialize(new { item.Id, item.Purpose, item.AmountPerMember }) });
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateManyAsync(recipients.Where(recipient => recipient.UserId != command.UserId)
            .Select(recipient => new CreateNotificationRequest(recipient.UserId, "GroupFundRequest",
                "Group contribution requested", purpose, new { item.Id, item.GroupId, item.AmountPerMember }))
            .ToList(), cancellationToken);
        return CurrentGroupMappings.FundRequest(item);
    }
}

public sealed class GetFundRequestsQueryHandler(ISpendWiseDbContext db, GroupAccess access, IClock clock)
    : IRequestHandler<GetFundRequestsQuery, IReadOnlyList<FundRequestDto>>
{
    public Task<IReadOnlyList<FundRequestDto>> Handle(GetFundRequestsQuery query, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(token => ReadAsync(query, token), cancellationToken);

    private async Task<IReadOnlyList<FundRequestDto>> ReadAsync(GetFundRequestsQuery query, CancellationToken cancellationToken)
    {
        // This query persists Overdue transitions, so it must cooperate with payment admission.
        await GroupFinancialConcurrency.SerializeAsync(db, query.GroupId, cancellationToken);
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var items = await db.FundRequests.Include(item => item.Recipients)
            .Where(item => item.GroupId == query.GroupId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var changed = false;
        foreach (var item in items.Where(item => item.DueDate < clock.UtcNow))
        {
            foreach (var recipient in item.Recipients.Where(recipient => recipient.Status == FundRequestRecipientStatus.Pending))
            {
                recipient.Status = FundRequestRecipientStatus.Overdue;
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(cancellationToken);
        return items.Select(CurrentGroupMappings.FundRequest).ToList();
    }
}

public sealed class PayFundRequestCommandHandler(
    ISpendWiseDbContext db,
    WalletFundAccounting accounting)
    : IRequestHandler<PayFundRequestCommand, GroupContributionDto>
{
    public Task<GroupContributionDto> Handle(PayFundRequestCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(async token =>
        {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, token);
        var recipient = await db.FundRequestRecipients.Include(item => item.FundRequest)
            .SingleOrDefaultAsync(item => item.FundRequestId == command.FundRequestId
                && item.FundRequest.GroupId == command.GroupId && item.UserId == command.UserId, token)
            ?? throw new NotFoundException("fund_request_recipient_not_found", "Fund request recipient not found.");
        if (recipient.Status == FundRequestRecipientStatus.Paid)
        {
            var existing = recipient.PaymentId is Guid contributionId
                ? await db.GroupContributions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == contributionId, token)
                : null;
            if (existing is null || existing.IdempotencyKey != command.Request.IdempotencyKey?.Trim())
                throw new ConflictException("fund_request_already_paid", "This fund request has already been paid.");
        }
        var result = await accounting.ContributeAsync(command.UserId, command.GroupId, command.Request.WalletId,
            recipient.FundRequest.AmountPerMember, recipient.FundRequest.Purpose, command.Request.IdempotencyKey,
            command.Request.SecurityPin, recipient.Id, token);
        recipient.Status = FundRequestRecipientStatus.Paid;
        recipient.PaymentId = result.Id;
        recipient.PaidAt ??= result.CreatedAt;
        await db.SaveChangesAsync(token);
        return result;
        }, cancellationToken);
}

public sealed class CreateFundRoundCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    INotificationService notifications)
    : IRequestHandler<CreateFundRoundCommand, FundRoundDto>
{
    public Task<FundRoundDto> Handle(CreateFundRoundCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(token => CreateAsync(command, token), cancellationToken);

    private async Task<FundRoundDto> CreateAsync(CreateFundRoundCommand command, CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        await access.RequireOwnerAsync(command.UserId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var title = InputBoundary.RequiredText(command.Request.Title, 160, "Title");
        var description = InputBoundary.OptionalText(command.Request.Description, 1000, "Description");
        InputBoundary.PositiveMoney(command.Request.TargetAmount);
        if (decimal.Truncate(command.Request.TargetAmount) != command.Request.TargetAmount)
        {
            throw new AppException("vnd_integer_required", "Fund round amount must be a whole VND amount.");
        }
        var members = await db.GroupMembers.AsNoTracking().Where(item => item.GroupId == command.GroupId)
            .OrderBy(item => item.UserId).Select(item => new { item.UserId, item.User.FullName }).ToListAsync(cancellationToken);
        var baseAmount = decimal.Floor(command.Request.TargetAmount / members.Count);
        var remainder = (int)(command.Request.TargetAmount - baseAmount * members.Count);
        var round = new GroupFundRound
        {
            GroupId = command.GroupId,
            Title = title,
            Description = description,
            TargetAmount = command.Request.TargetAmount,
            CreatedByUserId = command.UserId,
            Members = members.Select((member, index) => new GroupFundRoundMember
            {
                UserId = member.UserId,
                UserNameSnapshot = member.FullName,
                RequiredAmount = baseAmount + (index < remainder ? 1 : 0)
            }).ToList()
        };
        db.GroupFundRounds.Add(round);
        db.GroupActivities.Add(new GroupActivity { GroupId = command.GroupId, ActorUserId = command.UserId,
            Type = GroupActivityType.FundRoundCreated, DataJson = JsonSerializer.Serialize(new { round.Id, round.Title, round.TargetAmount }) });
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateManyAsync(members.Where(member => member.UserId != command.UserId)
            .Select(member => new CreateNotificationRequest(member.UserId, "GroupFundRound", "New group fund round",
                title, new { round.Id, round.GroupId, round.TargetAmount })).ToList(), cancellationToken);
        return CurrentGroupMappings.Round(round);
    }
}

public sealed class GetFundRoundsQueryHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<GetFundRoundsQuery, IReadOnlyList<FundRoundDto>>
{
    public async Task<IReadOnlyList<FundRoundDto>> Handle(GetFundRoundsQuery query, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var items = await db.GroupFundRounds.AsNoTracking().Include(item => item.Members)
            .Where(item => item.GroupId == query.GroupId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return items.Select(CurrentGroupMappings.Round).ToList();
    }
}

public sealed class PayFundRoundCommandHandler(ISpendWiseDbContext db, WalletFundAccounting accounting, IClock clock)
    : IRequestHandler<PayFundRoundCommand, GroupContributionDto>
{
    public Task<GroupContributionDto> Handle(PayFundRoundCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(async token =>
        {
        InputBoundary.PositiveMoney(command.Request.Amount);
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, token);
        var roundKey = InputBoundary.RequiredText(command.Request.IdempotencyKey, 100, "Idempotency key");
        if (decimal.Truncate(command.Request.Amount) != command.Request.Amount)
            throw new AppException("vnd_integer_required", "Fund round payment must be a whole VND amount.");
        var member = await db.GroupFundRoundMembers.Include(item => item.Round).SingleOrDefaultAsync(item =>
            item.GroupFundRoundId == command.RoundId && item.Round.GroupId == command.GroupId
            && item.UserId == command.UserId, token)
            ?? throw new NotFoundException("fund_round_member_not_found", "Fund round member not found.");
        var replay = await db.GroupContributions.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == command.UserId
            && item.IdempotencyKey == roundKey, token);
        if (replay is not null)
            return await accounting.ContributeAsync(command.UserId, command.GroupId, command.Request.WalletId,
                command.Request.Amount, member.Round.Title, command.Request.IdempotencyKey,
                command.Request.SecurityPin, member.Id, token);
        if (member.Round.Status != GroupFundRoundStatus.Active)
            throw new ConflictException("fund_round_completed", "Fund round is already completed.");
        var remaining = member.RequiredAmount - member.PaidAmount;
        if (command.Request.Amount > remaining)
            throw new AppException("fund_round_overpayment", "Payment exceeds the member's remaining obligation.");
        var result = await accounting.ContributeAsync(command.UserId, command.GroupId, command.Request.WalletId,
            command.Request.Amount, member.Round.Title, command.Request.IdempotencyKey,
            command.Request.SecurityPin, member.Id, token);
        if (!result.Replayed)
        {
            member.PaidAmount += command.Request.Amount;
            member.Status = member.PaidAmount == member.RequiredAmount
                ? GroupFundRoundMemberStatus.Paid : GroupFundRoundMemberStatus.Partial;
            member.Round.CollectedAmount += command.Request.Amount;
            var othersPaid = await db.GroupFundRoundMembers.Where(item => item.GroupFundRoundId == member.Round.Id
                    && item.Id != member.Id).AllAsync(item => item.Status == GroupFundRoundMemberStatus.Paid, token);
            if (member.Status == GroupFundRoundMemberStatus.Paid && othersPaid)
            {
                member.Round.Status = GroupFundRoundStatus.Completed;
                member.Round.CompletedAt = clock.UtcNow;
            }
            await db.SaveChangesAsync(token);
        }
        return result;
        }, cancellationToken);
}

public sealed class CreateCurrentGroupExpenseCommandHandler(ISpendWiseDbContext db, GroupAccess access, IClock clock,
    FinancialSecurityGuard security, WalletFundAccounting accounting, ILedgerService ledger)
    : IRequestHandler<CreateCurrentGroupExpenseCommand, GroupExpenseDto>
{
    public Task<GroupExpenseDto> Handle(CreateCurrentGroupExpenseCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(token => CreateAsync(command, token), cancellationToken);

    private async Task<GroupExpenseDto> CreateAsync(
        CreateCurrentGroupExpenseCommand command,
        CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        var membership = await access.RequireMemberAsync(command.UserId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var request = command.Request;
        var title = InputBoundary.RequiredText(request.Title, 160, "Title");
        InputBoundary.PositiveMoney(request.Amount);
        if (!Enum.TryParse<GroupExpensePaidByType>(request.PaidByType, true, out var paidBy) || !Enum.IsDefined(paidBy))
            throw new AppException("invalid_paid_by_type", "PaidByType must be user or fund.");
        string category;
        if (request.CategoryId is Guid categoryId)
        {
            category = await db.Categories.AsNoTracking().Where(item => item.Id == categoryId && item.IsActive
                && (item.IsSystem || item.OwnerUserId == command.UserId)).Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("category_not_found", "Category not found.");
        }
        else
        {
            category = InputBoundary.RequiredText(request.Category, 160, "Category");
        }
        var idempotencyKey = paidBy == GroupExpensePaidByType.Fund
            ? InputBoundary.RequiredText(request.IdempotencyKey, 100, "Idempotency key")
            : null;
        var description = InputBoundary.OptionalText(request.Description, 1000, "Description");
        var explicitDate = request.Date?.ToUniversalTime();
        if (idempotencyKey is not null)
        {
            var replay = await db.GroupExpenses.AsNoTracking().SingleOrDefaultAsync(item => item.GroupId == command.GroupId
                && item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (replay is not null)
            {
                if (!await ReplayMatchesAsync(replay, command.UserId, request.Amount, title, request.CategoryId,
                    category, description, explicitDate, cancellationToken))
                    throw new ConflictException("idempotency_conflict", "The idempotency key was used with different expense details.");
                return CurrentGroupMappings.Expense(replay);
            }
        }
        Guid? paidByUserId = request.PaidByUserId;
        SharedFund? fund = null;
        List<GroupFundShare>? shares = null;
        LedgerAccount? clearing = null;
        if (paidBy == GroupExpensePaidByType.Fund)
        {
            clearing = await GetExpenseClearingAsync(cancellationToken);
            fund = await accounting.LoadCompatibleFundAsync(command.GroupId, cancellationToken);
            if (fund.Balance < request.Amount)
                throw new ConflictException("group_fund_insufficient", "Group fund balance is insufficient.");
            shares = await db.GroupFundShares.Where(item => item.GroupId == command.GroupId && item.RemainingAmount > 0)
                .OrderBy(item => item.UserId).ToListAsync(cancellationToken);
            paidByUserId = null;
        }
        else
        {
            paidByUserId ??= command.UserId;
            var payerMember = await db.GroupMembers.AsNoTracking().AnyAsync(item => item.GroupId == command.GroupId
                && item.UserId == paidByUserId, cancellationToken);
            if (!payerMember || (membership.Role != GroupRole.Owner && paidByUserId != command.UserId))
                throw new ForbiddenException("group_expense_payer_invalid", "Paid-by user must be an allowed group member.");
        }
        var expense = new GroupExpense
        {
            GroupId = command.GroupId,
            Title = title,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            CategorySnapshot = category,
            PaidByType = paidBy,
            PaidByUserId = paidByUserId,
            Date = explicitDate ?? clock.UtcNow,
            Description = description,
            IdempotencyKey = idempotencyKey
        };
        if (paidBy == GroupExpensePaidByType.Fund)
        {
            var originalBalance = fund!.Balance;
            var posting = await ledger.PostAsync(fund.LedgerAccountId, clearing!.Id, request.Amount, "VND",
                "GroupFundExpense", expense.Id, $"group:{command.GroupId:N}:fund-expense", idempotencyKey, title, cancellationToken);
            if (posting.Replayed)
            {
                var replay = await db.GroupExpenses.AsNoTracking().SingleOrDefaultAsync(item => item.GroupId == command.GroupId
                    && item.IdempotencyKey == idempotencyKey, cancellationToken)
                    ?? throw new ConflictException("idempotency_conflict", "The original fund expense is not available for replay.");
                if (!await ReplayMatchesAsync(replay, command.UserId, request.Amount, title, request.CategoryId,
                    category, description, explicitDate, cancellationToken))
                    throw new ConflictException("idempotency_conflict", "The idempotency key was used with different expense details.");
                return CurrentGroupMappings.Expense(replay);
            }
            fund.Balance -= request.Amount;
            var remainingToAllocate = request.Amount;
            for (var index = 0; index < shares!.Count; index++)
            {
                var share = shares[index];
                var allocation = index == shares.Count - 1
                    ? remainingToAllocate
                    : decimal.Floor(request.Amount * share.RemainingAmount / originalBalance * 100m) / 100m;
                if (allocation > share.RemainingAmount)
                    allocation = share.RemainingAmount;
                share.RemainingAmount -= allocation;
                remainingToAllocate -= allocation;
                expense.ShareAllocations.Add(new GroupExpenseShareAllocation { UserId = share.UserId, Amount = allocation });
            }
            if (remainingToAllocate != 0)
                throw new ConflictException("group_share_allocation_failed", "Member shares cannot cover this expense exactly.");
            db.FundTransactions.Add(new FundTransaction { GroupId = command.GroupId, Type = FundTransactionType.Expense,
                Amount = request.Amount, UserId = command.UserId,
                UserNameSnapshot = await db.Users.Where(item => item.Id == command.UserId).Select(item => item.FullName).SingleAsync(cancellationToken),
                Description = title, Date = expense.Date,
                ReferenceCode = posting.Reference, LedgerTransactionId = posting.LedgerTransactionId });
        }
        // Chỉ thêm chi phí sau khi posting Ledger thành công, để LedgerService không lưu bản ghi dở dang.
        db.GroupExpenses.Add(expense);
        db.GroupActivities.Add(new GroupActivity { GroupId = command.GroupId, ActorUserId = command.UserId,
            Type = paidBy == GroupExpensePaidByType.Fund ? GroupActivityType.FundExpense : GroupActivityType.Expense,
            DataJson = JsonSerializer.Serialize(new { expense.Id, expense.Amount, expense.Title, paidByType = paidBy.ToString() }) });
        await db.SaveChangesAsync(cancellationToken);
        return CurrentGroupMappings.Expense(expense);
    }

    private async Task<bool> ReplayMatchesAsync(GroupExpense replay, Guid actorUserId, decimal amount,
        string title, Guid? categoryId, string category, string? description, DateTime? explicitDate,
        CancellationToken cancellationToken)
    {
        if (replay.Amount != amount || replay.Title != title || replay.CategoryId != categoryId
            || replay.CategorySnapshot != category || replay.Description != description
            || (explicitDate is not null && replay.Date != explicitDate.Value))
            return false;

        // Activity lưu actor và payload của chi phí; đọc payload rồi so sánh đúng trường id.
        var activities = await db.GroupActivities.AsNoTracking()
            .Where(item => item.GroupId == replay.GroupId && item.Type == GroupActivityType.FundExpense
                && item.ActorUserId == actorUserId && item.DataJson != null)
            .Select(item => item.DataJson!)
            .ToListAsync(cancellationToken);
        return activities.Any(data =>
        {
            try
            {
                using var document = JsonDocument.Parse(data);
                return document.RootElement.TryGetProperty("Id", out var id)
                    && id.ValueKind == JsonValueKind.String
                    && Guid.TryParse(id.GetString(), out var activityExpenseId)
                    && activityExpenseId == replay.Id;
            }
            catch (JsonException)
            {
                return false;
            }
        });
    }

    private async Task<LedgerAccount> GetExpenseClearingAsync(CancellationToken cancellationToken)
    {
        const string code = "SYSTEM:GroupExpenseClearing:VND";
        var account = await db.LedgerAccounts.SingleOrDefaultAsync(item => item.Code == code, cancellationToken);
        if (account is not null) return account;
        account = new LedgerAccount { Code = code, Name = "Group expense clearing", Type = LedgerAccountType.ExpenseClearing, IsSystem = true };
        db.LedgerAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);
        return account;
    }
}

public sealed class GetCurrentGroupExpensesQueryHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<GetCurrentGroupExpensesQuery, IReadOnlyList<GroupExpenseDto>>
{
    public async Task<IReadOnlyList<GroupExpenseDto>> Handle(GetCurrentGroupExpensesQuery query, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var items = await db.GroupExpenses.AsNoTracking().Where(item => item.GroupId == query.GroupId)
            .OrderByDescending(item => item.Date).ToListAsync(cancellationToken);
        return items.Select(CurrentGroupMappings.Expense).ToList();
    }
}

public sealed class GetGroupActivitiesQueryHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<GetGroupActivitiesQuery, IReadOnlyList<GroupActivityDto>>
{
    public async Task<IReadOnlyList<GroupActivityDto>> Handle(GetGroupActivitiesQuery query, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        return await db.GroupActivities.AsNoTracking().Where(item => item.GroupId == query.GroupId)
            .OrderByDescending(item => item.CreatedAt).Select(item => new GroupActivityDto(item.Id,
                item.ActorUserId, item.Type.ToString(), item.DataJson, item.CreatedAt)).ToListAsync(cancellationToken);
    }
}

public sealed class CreateGroupDeletionRequestCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    IClock clock,
    INotificationService notifications,
    GroupDeletionSupport deletion)
    : IRequestHandler<CreateGroupDeletionRequestCommand, GroupDeletionRequestDto>
{
    public Task<GroupDeletionRequestDto> Handle(CreateGroupDeletionRequestCommand command, CancellationToken cancellationToken) =>
        // Group freeze, vote/refund plan and persisted notifications must commit together.
        db.ExecuteInTransactionAsync(token => CreateAsync(command, token), cancellationToken);

    private async Task<GroupDeletionRequestDto> CreateAsync(CreateGroupDeletionRequestCommand command, CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        await access.RequireOwnerAsync(command.UserId, command.GroupId, cancellationToken);
        await access.EnsureMutableAsync(command.GroupId, cancellationToken);
        var members = await db.GroupMembers.AsNoTracking().Where(item => item.GroupId == command.GroupId)
            .OrderBy(item => item.UserId).Select(item => new { item.UserId, item.User.FullName }).ToListAsync(cancellationToken);
        var request = new GroupDeletionRequest
        {
            GroupId = command.GroupId,
            OwnerUserId = command.UserId,
            Votes = members.Select(member => new GroupDeletionVote
            {
                UserId = member.UserId,
                UserNameSnapshot = member.FullName,
                Status = member.UserId == command.UserId ? GroupDeletionVoteStatus.Approved : GroupDeletionVoteStatus.Pending,
                VotedAt = member.UserId == command.UserId ? clock.UtcNow : null
            }).ToList()
        };
        db.GroupDeletionRequests.Add(request);
        var group = await db.Groups.SingleAsync(item => item.Id == command.GroupId, cancellationToken);
        group.Status = GroupStatus.DeletionPending;
        await db.SaveChangesAsync(cancellationToken);
        await notifications.CreateManyAsync(members.Where(member => member.UserId != command.UserId)
            .Select(member => new CreateNotificationRequest(member.UserId, "GroupDeletionVote",
                "Group deletion vote", "The group owner requested deletion.", new { request.Id, request.GroupId }))
            .ToList(), cancellationToken);
        if (request.Votes.All(item => item.Status == GroupDeletionVoteStatus.Approved))
            await deletion.MakeReadyAsync(request, cancellationToken);
        return CurrentGroupMappings.Deletion(request);
    }
}

public sealed class GetGroupDeletionRequestQueryHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<GetGroupDeletionRequestQuery, GroupDeletionRequestDto?>
{
    public async Task<GroupDeletionRequestDto?> Handle(GetGroupDeletionRequestQuery query, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(query.UserId, query.GroupId, cancellationToken);
        var item = await db.GroupDeletionRequests.AsNoTracking().Include(request => request.Votes)
            .Include(request => request.Refunds).Where(request => request.GroupId == query.GroupId)
            .OrderByDescending(request => request.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        return item is null ? null : CurrentGroupMappings.Deletion(item);
    }
}

public sealed class VoteGroupDeletionCommandHandler(
    ISpendWiseDbContext db,
    IClock clock,
    INotificationService notifications,
    GroupDeletionSupport deletion,
    FinancialSecurityGuard security)
    : IRequestHandler<VoteGroupDeletionCommand, GroupDeletionRequestDto>
{
    public async Task<GroupDeletionRequestDto> Handle(VoteGroupDeletionCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        return await db.ExecuteInTransactionAsync(token => VoteAsync(command, token), cancellationToken);
    }

    private async Task<GroupDeletionRequestDto> VoteAsync(VoteGroupDeletionCommand command, CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        var request = await db.GroupDeletionRequests.Include(item => item.Votes).Include(item => item.Refunds)
            .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.GroupId == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("deletion_request_not_found", "Group deletion request not found.");
        if (request.Status != GroupDeletionRequestStatus.Pending)
            throw new ConflictException("deletion_vote_closed", "Deletion voting is no longer pending.");
        var vote = request.Votes.SingleOrDefault(item => item.UserId == command.UserId)
            ?? throw new ForbiddenException("deletion_vote_forbidden", "Only snapshotted members may vote.");
        if (vote.Status != GroupDeletionVoteStatus.Pending)
            throw new ConflictException("deletion_vote_recorded", "This member already voted.");
        vote.Status = command.Request.Approved ? GroupDeletionVoteStatus.Approved : GroupDeletionVoteStatus.Rejected;
        vote.VotedAt = clock.UtcNow;
        if (!command.Request.Approved)
        {
            request.Status = GroupDeletionRequestStatus.Rejected;
            var group = await db.Groups.SingleAsync(item => item.Id == command.GroupId, cancellationToken);
            group.Status = GroupStatus.Active;
            await db.SaveChangesAsync(cancellationToken);
            await notifications.CreateAsync(request.OwnerUserId, "GroupDeletionRejected", "Group deletion rejected",
                "A member rejected the deletion request.", new { request.Id, command.UserId }, cancellationToken);
        }
        else if (request.Votes.All(item => item.Status == GroupDeletionVoteStatus.Approved))
        {
            await deletion.MakeReadyAsync(request, cancellationToken);
        }
        else
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        return CurrentGroupMappings.Deletion(request);
    }
}

public sealed class CancelGroupDeletionCommandHandler(ISpendWiseDbContext db, GroupAccess access)
    : IRequestHandler<CancelGroupDeletionCommand, GroupDeletionRequestDto>
{
    public Task<GroupDeletionRequestDto> Handle(CancelGroupDeletionCommand command, CancellationToken cancellationToken) =>
        db.ExecuteInTransactionAsync(token => CancelAsync(command, token), cancellationToken);

    private async Task<GroupDeletionRequestDto> CancelAsync(CancelGroupDeletionCommand command, CancellationToken cancellationToken)
    {
        await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, cancellationToken);
        await access.RequireOwnerAsync(command.UserId, command.GroupId, cancellationToken);
        var request = await db.GroupDeletionRequests.Include(item => item.Votes).Include(item => item.Refunds)
            .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.GroupId == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("deletion_request_not_found", "Group deletion request not found.");
        if (request.Status is not (GroupDeletionRequestStatus.Pending or GroupDeletionRequestStatus.Ready)
            || request.Refunds.Any(item => item.Status != GroupRefundStatus.Pending))
            throw new ConflictException("deletion_cancel_unavailable", "Deletion can no longer be cancelled.");
        request.Status = GroupDeletionRequestStatus.Cancelled;
        var group = await db.Groups.SingleAsync(item => item.Id == command.GroupId, cancellationToken);
        group.Status = GroupStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return CurrentGroupMappings.Deletion(request);
    }
}

public sealed class CreateGroupRefundPaymentCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    INotificationService notifications,
    FinancialSecurityGuard security,
    WalletFundAccounting accounting,
    ILedgerService ledger,
    IClock clock)
    : IRequestHandler<CreateGroupRefundPaymentCommand, GroupRefundDto>
{
    public async Task<GroupRefundDto> Handle(CreateGroupRefundPaymentCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await GroupFinancialConcurrency.SerializeAsync(db, command.GroupId, token);
            var membership = await access.RequireMemberAsync(command.UserId, command.GroupId, token);
            var request = await db.GroupDeletionRequests.SingleOrDefaultAsync(item => item.Id == command.RequestId
                && item.GroupId == command.GroupId && item.Status == GroupDeletionRequestStatus.Ready, token)
                ?? throw new ConflictException("refund_plan_not_ready", "Refund plan is not ready.");
            var refund = await db.GroupRefunds.SingleOrDefaultAsync(item => item.Id == command.RefundId
                && item.GroupDeletionRequestId == request.Id, token)
                ?? throw new NotFoundException("group_refund_not_found", "Group refund not found.");
            if (command.UserId != refund.UserId && membership.Role != GroupRole.Owner)
                throw new ForbiddenException("group_refund_forbidden", "Only the member or group owner can select this refund wallet.");
            var walletQuery = db.Wallets.Include(item => item.LedgerAccount)
                .Where(item => item.OwnerUserId == refund.UserId && item.Status == WalletStatus.Active);
            var wallet = command.Request.WalletId is Guid walletId
                ? await walletQuery.SingleOrDefaultAsync(item => item.Id == walletId, token)
                : await walletQuery.Where(item => item.Type == WalletType.Main).SingleOrDefaultAsync(token);
            if (wallet is null)
                throw new ConflictException("refund_wallet_unavailable", "No eligible destination wallet is available.");
            var key = InputBoundary.RequiredText(command.Request.IdempotencyKey, 100, "Idempotency key");
            if (refund.Status == GroupRefundStatus.Completed)
            {
                if (refund.DestinationWalletId != wallet.Id || refund.IdempotencyKey != key)
                    throw new ConflictException("idempotency_conflict", "The refund was already completed with different details.");
                return CurrentGroupMappings.Refund(refund);
            }
            if (refund.RefundAmount <= 0)
                throw new ConflictException("group_refund_not_required", "This member has no refund to receive.");
            var fund = await accounting.LoadCompatibleFundAsync(command.GroupId, token);
            var share = await db.GroupFundShares.SingleAsync(item => item.GroupId == command.GroupId
                && item.UserId == refund.UserId, token);
            if (share.RemainingAmount != refund.RefundAmount)
                throw new ConflictException("refund_share_changed", "The member refund share changed.");
            var posting = await ledger.PostAsync(fund.LedgerAccountId, wallet.LedgerAccount.Id, refund.RefundAmount,
                "VND", "GroupRefund", refund.Id, $"group-refund:{request.Id:N}", key, "Group refund", token);
            fund.Balance -= refund.RefundAmount;
            share.RemainingAmount -= refund.RefundAmount;
            refund.Status = GroupRefundStatus.Completed;
            refund.CompletedAt = clock.UtcNow;
            refund.DestinationWalletId = wallet.Id;
            refund.LedgerTransactionId = posting.LedgerTransactionId;
            refund.IdempotencyKey = key;
            await db.SaveChangesAsync(token);
            await notifications.CreateAsync(refund.UserId, "GroupRefundCompleted", "Group refund completed",
                $"Your refund of {refund.RefundAmount:N0} VND was returned to your SpendWise wallet.",
                new { refundId = refund.Id, walletId = wallet.Id }, token);
            return CurrentGroupMappings.Refund(refund);
        }, cancellationToken);
    }
}

public sealed class FinalizeGroupDeletionCommandHandler(
    ISpendWiseDbContext db,
    GroupAccess access,
    FinancialSecurityGuard security,
    IClock clock)
    : IRequestHandler<FinalizeGroupDeletionCommand, GroupDeletionRequestDto>
{
    public async Task<GroupDeletionRequestDto> Handle(FinalizeGroupDeletionCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        return await db.ExecuteInTransactionAsync(async token =>
        {
            await access.RequireOwnerAsync(command.UserId, command.GroupId, token);
            var request = await db.GroupDeletionRequests.Include(item => item.Votes).Include(item => item.Refunds)
                .SingleOrDefaultAsync(item => item.Id == command.RequestId && item.GroupId == command.GroupId, token)
                ?? throw new NotFoundException("deletion_request_not_found", "Group deletion request not found.");
            if (request.Status != GroupDeletionRequestStatus.Ready)
                throw new ConflictException("deletion_not_ready", "Deletion request is not ready to finalize.");
            if (request.Refunds.Any(item => item.RefundAmount > 0 && item.Status != GroupRefundStatus.Completed))
                throw new ConflictException("refunds_incomplete", "All required refunds must be completed first.");
            var fund = await db.SharedFunds.Include(item => item.LedgerAccount)
                .SingleAsync(item => item.GroupId == command.GroupId, token);
            var remainingShares = await db.GroupFundShares.Where(item => item.GroupId == command.GroupId)
                .SumAsync(item => (decimal?)item.RemainingAmount, token) ?? 0;
            if (fund.Balance != 0 || fund.LedgerAccount.Balance != 0 || remainingShares != 0
                || request.Refunds.Sum(item => item.RefundAmount) != request.FundBalanceSnapshot)
                throw new ConflictException("refund_invariant_failed", "Group refund totals are inconsistent.");
            var group = await db.Groups.SingleAsync(item => item.Id == command.GroupId, token);
            request.Status = GroupDeletionRequestStatus.Completed;
            request.CompletedAt = clock.UtcNow;
            group.Status = GroupStatus.Closed;
            await db.SaveChangesAsync(token);
            return CurrentGroupMappings.Deletion(request);
        }, cancellationToken);
    }
}
