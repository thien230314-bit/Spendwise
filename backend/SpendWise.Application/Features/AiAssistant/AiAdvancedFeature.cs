using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Admin;
using SpendWise.Application.Banking;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Features.Admin.Cases.Commands.CreateSupportCase;
using SpendWise.Application.Features.Admin.Cases;
using SpendWise.Application.Features.Budgets.Commands.CreateBudget;
using SpendWise.Application.Features.Budgets.Commands.UpdateBudget;
using SpendWise.Application.Features.Budgets.Commands.DeleteBudget;
using SpendWise.Application.Features.Friends;
using SpendWise.Application.Features.Goals.Commands.ContributeGoal;
using SpendWise.Application.Features.Goals.Commands.CreateGoal;
using SpendWise.Application.Features.Goals.Commands.UpdateGoal;
using SpendWise.Application.Features.Goals.Commands.DeleteGoal;
using SpendWise.Application.Features.Goals.Commands.WithdrawGoal;
using SpendWise.Application.Features.Groups.Commands.CreateGroup;
using SpendWise.Application.Features.Groups;
using SpendWise.Application.Features.RecurringTransactions.Commands.RecordRecurringOccurrence;
using SpendWise.Application.Features.RecurringTransactions.Commands.CreateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.UpdateRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.PauseRecurringTransaction;
using SpendWise.Application.Features.RecurringTransactions.Commands.ResumeRecurringTransaction;
using SpendWise.Application.Features.Transactions.Commands.CreateExpense;
using SpendWise.Application.Features.Transactions.Commands.CreateIncome;
using SpendWise.Application.Features.Transactions.Commands.UpdateTransaction;
using SpendWise.Application.Features.Transfers.Commands.TransferBetweenWallets;
using SpendWise.Application.Features.BankDirectPayments;
using SpendWise.Application.Finance;
using SpendWise.Application.Groups;
using SpendWise.Application.Planning;
using SpendWise.Application.Social;
using SpendWise.Domain.Admin;
using SpendWise.Domain.AI;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant;

public sealed record UploadAiAttachmentCommand(Guid UserId, UploadAiAttachmentData Upload) : IRequest<AiAttachmentDto>;
public sealed record GetAiAttachmentQuery(Guid UserId, Guid AttachmentId) : IRequest<(string FileName, string ContentType, byte[] Content)>;
public sealed record GetAiActionQuery(Guid UserId, Guid ActionId) : IRequest<AiActionDto>;
public sealed record CancelAiActionCommand(Guid UserId, Guid ActionId) : IRequest<AiActionDto>;
public sealed record ExecuteAiActionCommand(Guid UserId, Guid ActionId, ExecuteAiActionRequest Request) : IRequest<AiActionDto>;

public sealed class UploadAiAttachmentCommandHandler(ISpendWiseDbContext db) : IRequestHandler<UploadAiAttachmentCommand, AiAttachmentDto>
{
    private const int MaximumBytes = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string[]> Allowed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = [".png"], ["image/jpeg"] = [".jpg", ".jpeg"], ["image/webp"] = [".webp"],
        ["application/pdf"] = [".pdf"], ["text/plain"] = [".txt"], ["text/csv"] = [".csv"],
        ["application/json"] = [".json"]
    };

    public async Task<AiAttachmentDto> Handle(UploadAiAttachmentCommand command, CancellationToken cancellationToken)
    {
        var upload = command.Upload;
        if (upload.Content.Length == 0 || upload.Content.Length > MaximumBytes)
            throw new AppException("ai_attachment_size_invalid", "AI attachment must be between 1 byte and 10 MB.");
        var fileName = Path.GetFileName(InputBoundary.RequiredText(upload.OriginalFileName, 255, "File name"));
        if (!string.Equals(fileName, upload.OriginalFileName, StringComparison.Ordinal) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new AppException("ai_attachment_name_invalid", "AI attachment file name is invalid.");
        var contentType = upload.ContentType.Trim().ToLowerInvariant();
        var extension = Path.GetExtension(fileName);
        if (!Allowed.TryGetValue(contentType, out var extensions) || !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new AppException("ai_attachment_unsupported", "AI attachment type is not supported.", 422);
        if (!SignatureMatches(contentType, upload.Content))
            throw new AppException("ai_attachment_signature_invalid", "AI attachment content does not match its declared type.");

        var attachment = new AiAttachment
        {
            OwnerUserId = command.UserId,
            OriginalFileName = fileName,
            ContentType = contentType,
            Length = upload.Content.LongLength,
            ContentHash = Convert.ToHexString(SHA256.HashData(upload.Content)).ToLowerInvariant(),
            Content = upload.Content
        };
        db.AiAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);
        return AiConversationMappings.ToAttachmentDto(attachment);
    }

    private static bool SignatureMatches(string contentType, byte[] content) => contentType switch
    {
        "image/png" => content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/jpeg" => content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        "image/webp" => content.Length >= 12 && content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && content.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        "application/pdf" => content.AsSpan().StartsWith("%PDF-"u8),
        "text/plain" or "text/csv" or "application/json" => !content.AsSpan().StartsWith("MZ"u8) && !content.Contains((byte)0),
        _ => false
    };
}

public sealed class GetAiAttachmentQueryHandler(ISpendWiseDbContext db)
    : IRequestHandler<GetAiAttachmentQuery, (string FileName, string ContentType, byte[] Content)>
{
    public async Task<(string FileName, string ContentType, byte[] Content)> Handle(GetAiAttachmentQuery query, CancellationToken cancellationToken)
    {
        var attachment = await db.AiAttachments.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == query.AttachmentId && x.OwnerUserId == query.UserId, cancellationToken)
            ?? throw new NotFoundException("ai_attachment_not_found", "AI attachment not found.");
        return (attachment.OriginalFileName, attachment.ContentType, attachment.Content);
    }
}

public sealed class AiToolRegistry(ISpendWiseDbContext db, IMediator mediator, IClock clock) : IAiToolRegistry
{
    private const string EmptySchema = "{\"type\":\"object\",\"properties\":{},\"additionalProperties\":false}";
    public IReadOnlyCollection<AiToolDefinition> Definitions { get; } =
    [
        Read("get_dashboard_summary", "Get the current user's balance and current-month spending."),
        Read("get_wallets", "List the current user's wallets."),
        Tool("get_wallet_balance", "Get one owned wallet balance.", "wallet_id"),
        Tool("search_transactions", "Search the current user's transactions.", "search"),
        Read("get_recent_transactions", "Get recent transactions for the current user."),
        Read("get_budget_status", "Get current budget configuration."), Read("get_goals", "Get savings goals."),
        Tool("get_goal_history", "Get movements for an owned goal.", "goal_id"),
        Read("get_recurring_items", "Get recurring definitions."), Read("get_due_recurring_occurrences", "Get due recurring occurrences."),
        Read("get_bank_connections", "Get bank connections."), Tool("get_bank_direct_payment_status", "Get an owned bank-direct payment status.", "payment_id"),
        Read("get_friends", "Get friends."), Read("get_groups", "Get active groups."),
        Tool("get_group_summary", "Get summary for a group where the user is an active member.", "group_id"),
        Tool("get_group_fund", "Get group fund where the user is an active member.", "group_id"),
        Tool("get_group_fund_requests", "Get group fund requests where the user is a member.", "group_id"),
        Tool("get_group_fund_rounds", "Get group fund rounds where the user is a member.", "group_id"),
        Tool("get_group_expenses", "Get group expenses where the user is a member.", "group_id"),
        Tool("get_group_activity", "Get group activity where the user is a member.", "group_id"),
        Read("get_own_support_cases", "Get the current user's support cases."), Read("get_own_disputes", "Get the current user's disputes."),
        Tool("send_friend_request", "Send a friend request. This is a direct non-financial action.", "contact"),
        Tool("accept_friend_request", "Accept a friend request.", "request_id"), Tool("reject_friend_request", "Reject a friend request.", "request_id"),
        Tool("create_support_case", "Create a support case.", "subject"),
        Direct("reply_support_case", "Reply to one support case owned by the current user.", SupportReplySchema),
        Direct("create_recurring", "Create a recurring definition without recording money.", RecurringCreateSchema),
        Direct("update_recurring", "Update a recurring definition without changing financial history.", RecurringUpdateSchema),
        Direct("pause_recurring", "Pause an owned recurring definition.", IdSchema("recurring_transaction_id")),
        Direct("resume_recurring", "Resume an owned recurring definition.", IdSchema("recurring_transaction_id")),
        Sensitive("prepare_manual_income", "Prepare an income transaction; execution requires PIN and evidence.", MoneySchema("wallet_id", "category_id", "amount", "description")),
        Sensitive("prepare_manual_expense", "Prepare an expense transaction; execution requires PIN and evidence.", MoneySchema("wallet_id", "category_id", "amount", "description")),
        Sensitive("prepare_internal_transfer", "Prepare an internal wallet transfer; execution requires PIN.", MoneySchema("source_wallet_id", "destination_wallet_id", "amount", "description")),
        Sensitive("prepare_budget_create", "Prepare a budget creation; execution requires PIN.", BudgetSchema),
        Sensitive("prepare_goal_create", "Prepare a savings goal creation; execution requires PIN.", GoalSchema),
        Sensitive("prepare_goal_contribution", "Prepare a goal contribution; execution requires PIN.", MoneySchema("goal_id", "wallet_id", "amount")),
        Sensitive("prepare_budget_update", "Prepare a budget update; execution requires PIN.", BudgetUpdateSchema),
        Sensitive("prepare_budget_delete", "Prepare a budget deletion; execution requires PIN.", IdSchema("budget_id")),
        Sensitive("prepare_goal_update", "Prepare goal metadata update; execution requires PIN.", GoalUpdateSchema),
        Sensitive("prepare_goal_delete", "Prepare an empty goal deletion; execution requires PIN.", IdSchema("goal_id")),
        Sensitive("prepare_goal_withdrawal", "Prepare a goal withdrawal; execution requires PIN.", MoneySchema("goal_id", "wallet_id", "amount")),
        Sensitive("prepare_recurring_occurrence_record", "Prepare recording a recurring occurrence; execution requires PIN and execute-time evidence.", RecurringOccurrenceSchema),
        Sensitive("prepare_transaction_correction", "Prepare an eligible transaction correction; execution requires PIN and evidence.", TransactionCorrectionSchema),
        Sensitive("prepare_bank_direct_sender_confirmation", "Prepare confirmation of a bank-direct payment; execution requires PIN.", IdSchema("payment_id")),
        Sensitive("prepare_group_create", "Prepare a group creation; execution requires PIN.", GroupCreateSchema),
        Sensitive("prepare_group_expense_create", "Prepare a group expense; execution requires PIN.", GroupExpenseSchema),
        Sensitive("prepare_group_deletion_vote", "Prepare a group deletion vote; execution requires PIN.", GroupDeletionVoteSchema),
        Sensitive("prepare_group_refund_payment_create", "Prepare a group refund payment; execution requires PIN.", GroupRefundSchema),
        Sensitive("prepare_group_deletion_finalize", "Prepare finalization of a group deletion; execution requires PIN.", GroupDeletionFinalizeSchema)
    ];

    public async Task<string> ExecuteAsync(Guid userId, Guid? conversationId, AiToolCall call, CancellationToken cancellationToken)
    {
        if (!Definitions.Any(x => x.Name == call.Name))
            throw new AppException("ai_tool_not_allowed", "The requested AI tool is not registered.", 403);
        using var document = ParseArguments(call.ArgumentsJson);
        var args = document.RootElement;
        ValidateDirectArguments(call.Name, args);
        object result = call.Name switch
        {
            "get_dashboard_summary" => await Dashboard(userId, cancellationToken),
            "get_wallets" => await db.Wallets.AsNoTracking().Where(x => x.OwnerUserId == userId)
                .Select(x => new { x.Id, x.Name, x.AvailableBalance, Status = x.Status.ToString() }).ToListAsync(cancellationToken),
            "get_wallet_balance" => await OwnedWallet(userId, RequiredGuid(args, "wallet_id"), cancellationToken),
            "search_transactions" => await Transactions(userId, OptionalString(args, "search"), cancellationToken),
            "get_recent_transactions" => await Transactions(userId, null, cancellationToken),
            "get_budget_status" => await db.Budgets.AsNoTracking().Where(x => x.OwnerUserId == userId)
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).Take(20)
                .Select(x => new { x.Id, x.Year, x.Month, x.LimitAmount, x.Rollover }).ToListAsync(cancellationToken),
            "get_goals" => await db.Goals.AsNoTracking().Where(x => x.OwnerUserId == userId)
                .Select(x => new { x.Id, x.Name, x.TargetAmount, x.CurrentAllocatedAmount, Status = x.Status.ToString() }).ToListAsync(cancellationToken),
            "get_goal_history" => await GoalHistory(userId, RequiredGuid(args, "goal_id"), cancellationToken),
            "get_recurring_items" => await db.RecurringTransactions.AsNoTracking().Where(x => x.OwnerUserId == userId)
                .Select(x => new { x.Id, x.Title, x.Description, x.Amount, x.NextRunAt, Status = x.Status.ToString() }).ToListAsync(cancellationToken),
            "get_due_recurring_occurrences" => await db.RecurringOccurrences.AsNoTracking()
                .Where(x => x.RecurringTransaction.OwnerUserId == userId && x.Status == SpendWise.Domain.Planning.RecurringOccurrenceStatus.Due)
                .Select(x => new { x.Id, x.ScheduledFor, x.RecurringTransaction.Title, x.RecurringTransaction.Description, x.RecurringTransaction.Amount }).ToListAsync(cancellationToken),
            "get_bank_connections" => await db.BankConnections.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.Id, x.BankBrandName, x.MaskedAccountNumber, Status = x.Status.ToString() }).ToListAsync(cancellationToken),
            "get_bank_direct_payment_status" => await BankPayment(userId, RequiredGuid(args, "payment_id"), cancellationToken),
            "get_friends" => await mediator.Send(new GetFriendsQuery(userId), cancellationToken),
            "get_groups" => await Groups(userId, cancellationToken),
            "get_group_summary" => await GroupSummary(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_group_fund" => await GroupFund(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_group_fund_requests" => await GroupRequests(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_group_fund_rounds" => await GroupRounds(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_group_expenses" => await GroupExpenses(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_group_activity" => await GroupActivity(userId, RequiredGuid(args, "group_id"), cancellationToken),
            "get_own_support_cases" => await db.SupportCases.AsNoTracking().Where(x => x.LinkedUserId == userId)
                .Select(x => new { x.Id, x.TicketNumber, x.Subject, Status = x.Status.ToString() }).ToListAsync(cancellationToken),
            "get_own_disputes" => await db.Disputes.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => new { x.Id, x.Reason, Status = x.Status.ToString(), x.LinkedTransactionId }).ToListAsync(cancellationToken),
            "send_friend_request" => await mediator.Send(new SendFriendRequestCommand(userId,
                new SendFriendRequest(RequiredString(args, "contact"), OptionalString(args, "message"))), cancellationToken),
            "accept_friend_request" => await mediator.Send(new AcceptFriendRequestCommand(userId, RequiredGuid(args, "request_id")), cancellationToken),
            "reject_friend_request" => await mediator.Send(new RejectFriendRequestCommand(userId, RequiredGuid(args, "request_id")), cancellationToken),
            "create_support_case" => await mediator.Send(new CreateSupportCaseCommand(userId, new CreateSupportCaseRequest(
                OptionalString(args, "category") ?? "general", OptionalString(args, "priority") ?? "Normal",
                args.TryGetProperty("linked_transaction_id", out var tx) && tx.TryGetGuid(out var txGuid) ? txGuid : null,
                RequiredString(args, "subject"), OptionalString(args, "description") ?? string.Empty)), cancellationToken),
            "reply_support_case" => await mediator.Send(new AddSupportMessageCommand(userId, false, RequiredGuid(args, "case_id"),
                new CaseMessageRequest(RequiredString(args, "message", 4000))), cancellationToken),
            "create_recurring" => await mediator.Send(new CreateRecurringTransactionCommand(userId,
                RecurringRequest(args)), cancellationToken),
            "update_recurring" => await mediator.Send(new UpdateRecurringTransactionCommand(userId,
                RequiredGuid(args, "recurring_transaction_id"), RecurringUpdateRequest(args)), cancellationToken),
            "pause_recurring" => await mediator.Send(new PauseRecurringTransactionCommand(userId,
                RequiredGuid(args, "recurring_transaction_id")), cancellationToken),
            "resume_recurring" => await mediator.Send(new ResumeRecurringTransactionCommand(userId,
                RequiredGuid(args, "recurring_transaction_id")), cancellationToken),
            _ when call.Name.StartsWith("prepare_", StringComparison.Ordinal) => await CreateProposal(userId, conversationId, call.Name[8..], args, cancellationToken),
            _ => throw new AppException("ai_tool_not_allowed", "The requested AI tool is not registered.", 403)
        };
        return JsonSerializer.Serialize(result);
    }

    private async Task<object> CreateProposal(Guid userId, Guid? conversationId, string actionType, JsonElement payload, CancellationToken ct)
    {
        var (payloadJson, safeSummary, requiresEvidence) = SanitizeProposal(actionType, payload);
        var proposal = new AiActionProposal
        {
            OwnerUserId = userId, AiConversationId = conversationId, ActionType = actionType,
            PayloadJson = payloadJson, SafeSummary = safeSummary,
            Status = AiActionProposalStatus.Pending, ExpiresAt = clock.UtcNow.AddMinutes(10), OperationKey = $"ai-{Guid.NewGuid():N}"
        };
        db.AiActionProposals.Add(proposal);
        await db.SaveChangesAsync(ct);
        return new AiProposedActionDto(proposal.Id, actionType, proposal.SafeSummary, true,
            requiresEvidence, true, proposal.ExpiresAt);
    }

    private async Task<object> Dashboard(Guid userId, CancellationToken ct)
    {
        var localNow = VietnamBusinessTime.ToLocal(clock.UtcNow);
        var (startUtc, endUtc) = VietnamBusinessTime.MonthUtcRange(localNow.Year, localNow.Month);
        return new
        {
            TotalBalance = await db.Wallets.Where(x => x.OwnerUserId == userId && x.Status != WalletStatus.Closed)
                .SumAsync(x => (decimal?)x.AvailableBalance, ct) ?? 0,
            CurrentMonthExpense = await db.FinancialTransactions.Where(x => x.OwnerUserId == userId
                    && x.Type == FinancialTransactionType.Expense
                    && x.OccurredAt >= startUtc && x.OccurredAt < endUtc)
                .SumAsync(x => (decimal?)x.Amount, ct) ?? 0
        };
    }
    private async Task<object> OwnedWallet(Guid userId, Guid id, CancellationToken ct) => await db.Wallets.AsNoTracking()
        .Where(x => x.Id == id && x.OwnerUserId == userId).Select(x => new { x.Id, x.Name, x.AvailableBalance, Status = x.Status.ToString() })
        .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("wallet_not_found", "Wallet not found.");
    private async Task<object> Transactions(Guid userId, string? search, CancellationToken ct) => await db.FinancialTransactions.AsNoTracking()
        .Where(x => x.OwnerUserId == userId && (search == null || x.Description.Contains(search)))
        .OrderByDescending(x => x.OccurredAt).Take(20).Select(x => new { x.Id, Type = x.Type.ToString(), x.Amount, x.Description, x.OccurredAt }).ToListAsync(ct);
    private async Task<object> GoalHistory(Guid userId, Guid id, CancellationToken ct) => await db.GoalMovements.AsNoTracking()
        .Where(x => x.GoalId == id && x.Goal.OwnerUserId == userId).OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, Type = x.Type.ToString(), x.Amount, x.CreatedAt }).ToListAsync(ct);
    private async Task<object> BankPayment(Guid userId, Guid id, CancellationToken ct) => await db.BankDirectPayments.AsNoTracking()
        .Where(x => x.Id == id && (x.SenderUserId == userId || x.RecipientUserId == userId))
        .Select(x => new { x.Id, x.Amount, Status = x.Status.ToString(), x.PaymentReference }).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("payment_not_found", "Payment not found.");
    private async Task<object> Groups(Guid userId, CancellationToken ct) => await db.GroupMembers.AsNoTracking()
        .Where(x => x.UserId == userId && x.Group.Status == GroupStatus.Active).Select(x => new { x.GroupId, x.Group.Name, Role = x.Role.ToString() }).ToListAsync(ct);
    private async Task EnsureGroup(Guid userId, Guid groupId, CancellationToken ct)
    {
        if (!await db.GroupMembers.AnyAsync(x => x.GroupId == groupId && x.UserId == userId && x.Group.Status == GroupStatus.Active, ct))
            throw new ForbiddenException("group_membership_required", "Active group membership is required.");
    }
    private async Task<object> GroupSummary(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.Groups.AsNoTracking().Where(x => x.Id == groupId).Select(x => new { x.Id, x.Name, Members = x.Members.Count }).SingleAsync(ct); }
    private async Task<object> GroupFund(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.SharedFunds.AsNoTracking().Where(x => x.GroupId == groupId).Select(x => new { x.Id, x.Balance, x.Currency }).SingleAsync(ct); }
    private async Task<object> GroupRequests(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.FundRequests.AsNoTracking().Where(x => x.GroupId == groupId).Select(x => new { x.Id, x.Purpose, x.AmountPerMember, x.DueDate }).ToListAsync(ct); }
    private async Task<object> GroupRounds(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.GroupFundRounds.AsNoTracking().Where(x => x.GroupId == groupId).Select(x => new { x.Id, x.Title, x.TargetAmount, x.CollectedAmount, Status = x.Status.ToString() }).ToListAsync(ct); }
    private async Task<object> GroupExpenses(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.GroupExpenses.AsNoTracking().Where(x => x.GroupId == groupId).OrderByDescending(x => x.Date).Take(50).Select(x => new { x.Id, x.Title, x.Amount, x.CategoryId, x.PaidByUserId, x.Date, Status = x.Status.ToString() }).ToListAsync(ct); }
    private async Task<object> GroupActivity(Guid userId, Guid groupId, CancellationToken ct) { await EnsureGroup(userId, groupId, ct); return await db.GroupActivities.AsNoTracking().Where(x => x.GroupId == groupId).OrderByDescending(x => x.CreatedAt).Take(50).Select(x => new { x.Id, x.Type, x.DataJson, x.CreatedAt }).ToListAsync(ct); }

    private static JsonDocument ParseArguments(string json) { try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); } catch (JsonException) { throw new AppException("ai_tool_arguments_invalid", "AI tool arguments are invalid."); } }
    private static void ValidateDirectArguments(string toolName, JsonElement payload)
    {
        (string[] Allowed, string[] Required)? rule = toolName switch
        {
            "send_friend_request" => (["contact", "message"], ["contact"]),
            "accept_friend_request" or "reject_friend_request" => (["request_id"], ["request_id"]),
            "create_support_case" => (["subject", "description", "category", "priority", "linked_transaction_id"], ["subject"]),
            "reply_support_case" => (["case_id", "message"], ["case_id", "message"]),
            "create_recurring" => (["title", "description", "wallet_id", "category_id", "kind", "amount", "frequency", "next_run_at", "is_subscription"],
                ["title", "description", "wallet_id", "category_id", "kind", "amount", "frequency", "next_run_at"]),
            "update_recurring" => (["recurring_transaction_id", "title", "description", "wallet_id", "category_id", "kind", "amount", "frequency", "next_run_at", "is_subscription"],
                ["recurring_transaction_id", "title", "description", "wallet_id", "category_id", "kind", "amount", "frequency", "next_run_at"]),
            "pause_recurring" or "resume_recurring" => (["recurring_transaction_id"], ["recurring_transaction_id"]),
            _ => null
        };
        if (rule is null) return;
        foreach (var property in payload.EnumerateObject())
            if (!rule.Value.Allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new AppException("ai_tool_arguments_invalid", "AI tool contains an unexpected argument.");
        foreach (var required in rule.Value.Required)
            if (!payload.TryGetProperty(required, out _))
                throw new AppException("ai_tool_arguments_invalid", $"{required} is required.");
    }
    private static Guid RequiredGuid(JsonElement root, string name) => Guid.TryParse(OptionalString(root, name), out var value) ? value : throw new AppException("ai_tool_arguments_invalid", $"{name} must be a valid id.");
    private static string RequiredString(JsonElement root, string name, int maximumLength = 500) =>
        InputBoundary.RequiredText(OptionalString(root, name), maximumLength, name);
    private static string? OptionalString(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static decimal RequiredDecimal(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetDecimal(out var number) && number > 0
        ? number : throw new AppException("ai_tool_arguments_invalid", $"{name} must be a positive number.");
    private static bool OptionalBool(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
    private static DateTime RequiredDate(JsonElement root, string name) => IsoDateTimeParser.TryParseUtc(OptionalString(root, name), out var value)
        ? value : throw new AppException("ai_tool_arguments_invalid", $"{name} must be a valid date.");
    private static CreateRecurringRequest RecurringRequest(JsonElement root) => new(
        RequiredGuid(root, "wallet_id"), RequiredGuid(root, "category_id"), RequiredString(root, "kind"),
        RequiredDecimal(root, "amount"), RequiredString(root, "description"), RequiredString(root, "frequency"),
        RequiredDate(root, "next_run_at"), OptionalBool(root, "is_subscription"), RequiredString(root, "title"));
    private static UpdateRecurringRequest RecurringUpdateRequest(JsonElement root) => new(
        RequiredGuid(root, "wallet_id"), RequiredGuid(root, "category_id"), RequiredString(root, "kind"),
        RequiredDecimal(root, "amount"), RequiredString(root, "description"), RequiredString(root, "frequency"),
        RequiredDate(root, "next_run_at"), OptionalBool(root, "is_subscription"), RequiredString(root, "title"));
    private static (string Json, string Summary, bool RequiresEvidence) SanitizeProposal(string action, JsonElement payload)
    {
        var (allowed, required) = action switch
        {
            "manual_income" or "manual_expense" => (new[] { "wallet_id", "category_id", "amount", "description" }, new[] { "wallet_id", "category_id", "amount", "description" }),
            "internal_transfer" => (new[] { "source_wallet_id", "destination_wallet_id", "amount", "description" }, new[] { "source_wallet_id", "destination_wallet_id", "amount", "description" }),
            "budget_create" => (new[] { "category_id", "year", "month", "limit_amount", "alerts_enabled", "rollover" }, new[] { "category_id", "year", "month", "limit_amount" }),
            "budget_update" => (new[] { "budget_id", "limit_amount", "alerts_enabled", "rollover" }, new[] { "budget_id", "limit_amount" }),
            "budget_delete" => (new[] { "budget_id" }, new[] { "budget_id" }),
            "goal_create" => (new[] { "name", "target_amount", "target_date", "category_id", "icon_name", "color_hex", "initial_amount", "wallet_id" }, new[] { "name", "target_amount" }),
            "goal_update" => (new[] { "goal_id", "name", "target_amount", "target_date", "category_id", "icon_name", "color_hex" }, new[] { "goal_id", "name", "target_amount" }),
            "goal_delete" => (new[] { "goal_id" }, new[] { "goal_id" }),
            "goal_contribution" or "goal_withdrawal" => (new[] { "goal_id", "wallet_id", "amount" }, new[] { "goal_id", "wallet_id", "amount" }),
            "recurring_occurrence_record" => (new[] { "recurring_transaction_id", "occurrence_id", "idempotency_key" }, new[] { "recurring_transaction_id", "occurrence_id", "idempotency_key" }),
            "transaction_correction" => (new[] { "transaction_id", "category_id", "wallet_id", "amount", "description", "reason", "occurred_at", "counterparty" }, new[] { "transaction_id", "category_id", "wallet_id", "amount", "description", "reason", "occurred_at" }),
            "bank_direct_sender_confirmation" => (new[] { "payment_id" }, new[] { "payment_id" }),
            "group_create" => (new[] { "name", "description", "type", "currency" }, new[] { "name", "description", "type", "currency" }),
            "group_expense_create" => (new[] { "group_id", "title", "amount", "category_id", "paid_by_type", "paid_by_user_id", "date", "description" }, new[] { "group_id", "title", "amount", "category_id", "paid_by_type", "date" }),
            "group_deletion_vote" => (new[] { "group_id", "request_id", "approved" }, new[] { "group_id", "request_id", "approved" }),
            "group_refund_payment_create" => (new[] { "group_id", "request_id", "refund_id", "idempotency_key" }, new[] { "group_id", "request_id", "refund_id", "idempotency_key" }),
            "group_deletion_finalize" => (new[] { "group_id", "request_id" }, new[] { "group_id", "request_id" }),
            _ => throw new AppException("ai_action_unsupported", "This AI action type is not supported.", 422)
        };
        foreach (var property in payload.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw new AppException("ai_tool_arguments_invalid", "AI action contains an unexpected argument.");
        var node = new JsonObject();
        foreach (var name in allowed)
        {
            if (!payload.TryGetProperty(name, out var value))
            {
                if (required.Contains(name, StringComparer.Ordinal))
                    throw new AppException("ai_tool_arguments_invalid", $"{name} is required.");
                continue;
            }
            switch (name)
            {
                case "wallet_id": case "category_id": case "source_wallet_id": case "destination_wallet_id": case "goal_id":
                case "budget_id": case "payment_id": case "request_id": case "refund_id": case "recurring_transaction_id":
                case "occurrence_id": case "group_id": case "transaction_id": case "paid_by_user_id":
                    if (value.ValueKind != JsonValueKind.String || !Guid.TryParse(value.GetString(), out var id)) throw new AppException("ai_tool_arguments_invalid", $"{name} must be a valid id.");
                    node[name] = id.ToString(); break;
                case "amount": case "limit_amount": case "target_amount":
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var money) || money <= 0 || money > 1_000_000_000_000m) throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                    node[name] = money; break;
                case "initial_amount":
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var initialAmount) || initialAmount < 0 || initialAmount > 1_000_000_000_000m) throw new AppException("ai_tool_arguments_invalid", "initial_amount is invalid.");
                    node[name] = initialAmount; break;
                case "year":
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var year) || year is < 2000 or > 2200) throw new AppException("ai_tool_arguments_invalid", "year is invalid.");
                    node[name] = year; break;
                case "month":
                    if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var month) || month is < 1 or > 12) throw new AppException("ai_tool_arguments_invalid", "month is invalid.");
                    node[name] = month; break;
                case "description": case "name": case "icon_name": case "color_hex":
                    if (value.ValueKind != JsonValueKind.String) throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                    var text = value.GetString()!.Trim();
                    if (text.Length is < 1 or > 500 || (name == "icon_name" && text.Length > 50) || (name == "color_hex" && text.Length > 20)) throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                    node[name] = text; break;
                case "title": case "reason": case "type": case "currency": case "paid_by_type": case "idempotency_key": case "counterparty": case "occurred_at": case "target_date": case "date":
                    if (value.ValueKind != JsonValueKind.String) throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                    var textValue = value.GetString()!.Trim();
                    if (textValue.Length is < 1 or > 500 || (name == "idempotency_key" && textValue.Length > 100)) throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                    if (name is "occurred_at" or "target_date" or "date")
                    {
                        if (!IsoDateTimeParser.TryParseUtc(textValue, out var parsedDate))
                            throw new AppException("ai_tool_arguments_invalid", $"{name} is invalid.");
                        textValue = parsedDate.ToString("O");
                    }
                    if (name == "paid_by_type" && textValue.ToLowerInvariant() is not ("fund" or "user")) throw new AppException("ai_tool_arguments_invalid", "paid_by_type is invalid.");
                    node[name] = textValue; break;
                case "approved": case "alerts_enabled": case "rollover":
                    if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new AppException("ai_tool_arguments_invalid", "approved is invalid.");
                    node[name] = value.GetBoolean(); break;
            }
        }
        if ((node["initial_amount"]?.GetValue<decimal>() ?? 0) > 0 && node["wallet_id"] is null)
            throw new AppException("ai_tool_arguments_invalid", "wallet_id is required when initial_amount is greater than zero.");
        if (action == "group_expense_create" && node["paid_by_type"]!.GetValue<string>().Equals("user", StringComparison.OrdinalIgnoreCase) && node["paid_by_user_id"] is null)
            throw new AppException("ai_tool_arguments_invalid", "paid_by_user_id is required when paid_by_type is user.");
        var amount = node["amount"]?.GetValue<decimal>() ?? node["limit_amount"]?.GetValue<decimal>() ?? node["target_amount"]?.GetValue<decimal>() ?? 0;
        var summary = action switch
        {
            "manual_income" => $"Prepare income of {amount:N0} VND.",
            "manual_expense" => $"Prepare expense of {amount:N0} VND.",
            "internal_transfer" => $"Prepare an internal transfer of {amount:N0} VND.",
            "budget_create" => $"Prepare a budget of {amount:N0} VND.",
            "goal_create" => $"Prepare goal '{node["name"]!.GetValue<string>()}' with target {amount:N0} VND.",
            "goal_contribution" => $"Prepare a goal contribution of {amount:N0} VND.",
            "goal_withdrawal" => $"Prepare a goal withdrawal of {amount:N0} VND.",
            "budget_update" => $"Prepare a budget update to {amount:N0} VND.",
            "budget_delete" => "Prepare a budget configuration deletion.",
            "goal_update" => $"Prepare goal metadata update for '{node["name"]!.GetValue<string>()}'.",
            "goal_delete" => "Prepare a goal deletion.",
            "recurring_occurrence_record" => "Prepare recording a recurring occurrence.",
            "transaction_correction" => "Prepare a transaction correction.",
            "bank_direct_sender_confirmation" => "Prepare bank-direct payment confirmation.",
            "group_create" => $"Prepare group '{node["name"]!.GetValue<string>()}'.",
            "group_expense_create" => $"Prepare a group expense of {amount:N0} VND.",
            "group_deletion_vote" => "Prepare a group deletion vote.",
            "group_refund_payment_create" => "Prepare a group refund payment.",
            "group_deletion_finalize" => "Prepare finalization of a group deletion.",
            _ => "Prepare a SpendWise action."
        };
        return (node.ToJsonString(), summary, action is "manual_income" or "manual_expense" or "recurring_occurrence_record");
    }
    private static string MoneySchema(params string[] identifiers)
    {
        var properties = new JsonObject();
        foreach (var identifier in identifiers)
            properties[identifier] = identifier is "amount" ? new JsonObject { ["type"] = "number" }
                : identifier is "description" ? new JsonObject { ["type"] = "string", ["maxLength"] = 500 }
                : new JsonObject { ["type"] = "string", ["format"] = "uuid" };
        var required = new JsonArray(identifiers.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required, ["additionalProperties"] = false }.ToJsonString();
    }
    private const string BudgetSchema = "{\"type\":\"object\",\"properties\":{\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"year\":{\"type\":\"integer\",\"minimum\":2000,\"maximum\":2200},\"month\":{\"type\":\"integer\",\"minimum\":1,\"maximum\":12},\"limit_amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"alerts_enabled\":{\"type\":\"boolean\"},\"rollover\":{\"type\":\"boolean\"}},\"required\":[\"category_id\",\"year\",\"month\",\"limit_amount\"],\"additionalProperties\":false}";
    private const string BudgetUpdateSchema = "{\"type\":\"object\",\"properties\":{\"budget_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"limit_amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"alerts_enabled\":{\"type\":\"boolean\"},\"rollover\":{\"type\":\"boolean\"}},\"required\":[\"budget_id\",\"limit_amount\"],\"additionalProperties\":false}";
    private const string GoalSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"target_amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"target_date\":{\"type\":\"string\",\"format\":\"date-time\"},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"icon_name\":{\"type\":\"string\",\"maxLength\":50},\"color_hex\":{\"type\":\"string\",\"maxLength\":20},\"initial_amount\":{\"type\":\"number\",\"minimum\":0},\"wallet_id\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"name\",\"target_amount\"],\"additionalProperties\":false}";
    private const string GoalUpdateSchema = "{\"type\":\"object\",\"properties\":{\"goal_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"target_amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"target_date\":{\"type\":\"string\",\"format\":\"date-time\"},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"icon_name\":{\"type\":\"string\",\"maxLength\":50},\"color_hex\":{\"type\":\"string\",\"maxLength\":20}},\"required\":[\"goal_id\",\"name\",\"target_amount\"],\"additionalProperties\":false}";
    private const string RecurringOccurrenceSchema = "{\"type\":\"object\",\"properties\":{\"recurring_transaction_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"occurrence_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"idempotency_key\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":100}},\"required\":[\"recurring_transaction_id\",\"occurrence_id\",\"idempotency_key\"],\"additionalProperties\":false}";
    private const string TransactionCorrectionSchema = "{\"type\":\"object\",\"properties\":{\"transaction_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"wallet_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"description\":{\"type\":\"string\",\"maxLength\":500},\"reason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":1000},\"occurred_at\":{\"type\":\"string\",\"format\":\"date-time\"},\"counterparty\":{\"type\":\"string\",\"maxLength\":160}},\"required\":[\"transaction_id\",\"category_id\",\"wallet_id\",\"amount\",\"description\",\"reason\",\"occurred_at\"],\"additionalProperties\":false}";
    private const string GroupCreateSchema = "{\"type\":\"object\",\"properties\":{\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"description\":{\"type\":\"string\",\"maxLength\":1000},\"type\":{\"type\":\"string\",\"maxLength\":40},\"currency\":{\"type\":\"string\",\"const\":\"VND\"}},\"required\":[\"name\",\"description\",\"type\",\"currency\"],\"additionalProperties\":false}";
    private const string GroupExpenseSchema = "{\"type\":\"object\",\"properties\":{\"group_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"title\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"paid_by_type\":{\"type\":\"string\",\"enum\":[\"fund\",\"user\"]},\"paid_by_user_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"date\":{\"type\":\"string\",\"format\":\"date-time\"},\"description\":{\"type\":\"string\",\"maxLength\":1000}},\"required\":[\"group_id\",\"title\",\"amount\",\"category_id\",\"paid_by_type\",\"date\"],\"additionalProperties\":false}";
    private const string SupportReplySchema = "{\"type\":\"object\",\"properties\":{\"case_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"message\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":4000}},\"required\":[\"case_id\",\"message\"],\"additionalProperties\":false}";
    private const string RecurringCreateSchema = "{\"type\":\"object\",\"properties\":{\"title\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"description\":{\"type\":\"string\",\"maxLength\":500},\"wallet_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"kind\":{\"type\":\"string\",\"enum\":[\"Income\",\"Expense\"]},\"amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"frequency\":{\"type\":\"string\",\"enum\":[\"Daily\",\"Weekly\",\"Monthly\",\"Yearly\"]},\"next_run_at\":{\"type\":\"string\",\"format\":\"date-time\"},\"is_subscription\":{\"type\":\"boolean\"}},\"required\":[\"title\",\"description\",\"wallet_id\",\"category_id\",\"kind\",\"amount\",\"frequency\",\"next_run_at\"],\"additionalProperties\":false}";
    private const string RecurringUpdateSchema = "{\"type\":\"object\",\"properties\":{\"recurring_transaction_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"title\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"description\":{\"type\":\"string\",\"maxLength\":500},\"wallet_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"category_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"kind\":{\"type\":\"string\",\"enum\":[\"Income\",\"Expense\"]},\"amount\":{\"type\":\"number\",\"exclusiveMinimum\":0},\"frequency\":{\"type\":\"string\",\"enum\":[\"Daily\",\"Weekly\",\"Monthly\",\"Yearly\"]},\"next_run_at\":{\"type\":\"string\",\"format\":\"date-time\"},\"is_subscription\":{\"type\":\"boolean\"}},\"required\":[\"recurring_transaction_id\",\"title\",\"description\",\"wallet_id\",\"category_id\",\"kind\",\"amount\",\"frequency\",\"next_run_at\"],\"additionalProperties\":false}";
    private const string GroupDeletionVoteSchema = "{\"type\":\"object\",\"properties\":{\"group_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"request_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"approved\":{\"type\":\"boolean\"}},\"required\":[\"group_id\",\"request_id\",\"approved\"],\"additionalProperties\":false}";
    private const string GroupRefundSchema = "{\"type\":\"object\",\"properties\":{\"group_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"request_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"refund_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"idempotency_key\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":100}},\"required\":[\"group_id\",\"request_id\",\"refund_id\",\"idempotency_key\"],\"additionalProperties\":false}";
    private const string GroupDeletionFinalizeSchema = "{\"type\":\"object\",\"properties\":{\"group_id\":{\"type\":\"string\",\"format\":\"uuid\"},\"request_id\":{\"type\":\"string\",\"format\":\"uuid\"}},\"required\":[\"group_id\",\"request_id\"],\"additionalProperties\":false}";
    private static string IdSchema(string name) => $"{{\"type\":\"object\",\"properties\":{{\"{name}\":{{\"type\":\"string\",\"format\":\"uuid\"}}}},\"required\":[\"{name}\"],\"additionalProperties\":false}}";
    private static AiToolDefinition Read(string name, string description) => new(name, description, EmptySchema);
    private static AiToolDefinition Tool(string name, string description, string required)
    {
        // Mỗi tool chỉ nhận đúng các trường mà handler cần; không cho model chèn dữ liệu bí mật.
        var properties = name switch
        {
            "send_friend_request" => "\"contact\":{\"type\":\"string\",\"maxLength\":160},\"message\":{\"type\":\"string\",\"maxLength\":300}",
            "create_support_case" => "\"subject\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":200},\"description\":{\"type\":\"string\",\"maxLength\":2000},\"category\":{\"type\":\"string\",\"maxLength\":80},\"priority\":{\"type\":\"string\",\"maxLength\":40},\"linked_transaction_id\":{\"type\":\"string\",\"format\":\"uuid\"}",
            "search_transactions" => "\"search\":{\"type\":\"string\",\"maxLength\":160}",
            _ => $"\"{required}\":{{\"type\":\"string\",\"format\":\"uuid\"}}"
        };
        return new AiToolDefinition(name, description, $"{{\"type\":\"object\",\"properties\":{{{properties}}},\"required\":[\"{required}\"],\"additionalProperties\":false}}");
    }
    private static AiToolDefinition Sensitive(string name, string description, string schema) => new(name, description, schema);
    private static AiToolDefinition Direct(string name, string description, string schema) => new(name, description, schema);
}

public sealed class GetAiActionQueryHandler(ISpendWiseDbContext db) : IRequestHandler<GetAiActionQuery, AiActionDto>
{
    public async Task<AiActionDto> Handle(GetAiActionQuery query, CancellationToken ct) => Map(await Owned(db, query.UserId, query.ActionId, ct));
    internal static async Task<AiActionProposal> Owned(ISpendWiseDbContext db, Guid userId, Guid id, CancellationToken ct) =>
        await db.AiActionProposals.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
        ?? throw new NotFoundException("ai_action_not_found", "AI action was not found.");
    internal static AiActionDto Map(AiActionProposal x, object? result = null) => new(x.Id, x.ActionType, x.SafeSummary, x.Status.ToString(), x.ExpiresAt, x.ExecutedAt, x.FailureCode, result);
}

public sealed class CancelAiActionCommandHandler(ISpendWiseDbContext db) : IRequestHandler<CancelAiActionCommand, AiActionDto>
{
    public async Task<AiActionDto> Handle(CancelAiActionCommand command, CancellationToken ct)
    {
        // Hủy bằng compare-and-set để không thắng đồng thời với Execute.
        db.ClearTrackedEntities();
        var changed = await db.AiActionProposals
            .Where(x => x.Id == command.ActionId && x.OwnerUserId == command.UserId && x.Status == AiActionProposalStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, AiActionProposalStatus.Cancelled), ct);
        if (changed != 1) throw new ConflictException("ai_action_not_pending", "AI action is no longer pending.");
        var proposal = await GetAiActionQueryHandler.Owned(db, command.UserId, command.ActionId, ct);
        return GetAiActionQueryHandler.Map(proposal);
    }
}

public sealed class ExecuteAiActionCommandHandler(ISpendWiseDbContext db, IMediator mediator, IClock clock)
    : IRequestHandler<ExecuteAiActionCommand, AiActionDto>
{
    public async Task<AiActionDto> Handle(ExecuteAiActionCommand command, CancellationToken ct)
    {
        var snapshot = await GetAiActionQueryHandler.Owned(db, command.UserId, command.ActionId, ct);
        if (snapshot.Status != AiActionProposalStatus.Pending) throw new ConflictException("ai_action_not_pending", "AI action is no longer pending.");
        if (snapshot.ExpiresAt <= clock.UtcNow)
        {
            snapshot.Status = AiActionProposalStatus.Expired;
            await db.SaveChangesAsync(ct);
            throw new ConflictException("ai_action_expired", "AI action has expired.");
        }
        if (!command.Request.ExplicitConfirmation) throw new AppException("explicit_confirmation_required", "Explicit confirmation is required.");
        var pin = InputBoundary.RequiredText(command.Request.SecurityPin, 6, "Security PIN");
        // Compare-and-set chuyển Pending sang Executing trước khi chạy command tài chính.
        db.ClearTrackedEntities();
        var claimed = await db.AiActionProposals
            .Where(x => x.Id == command.ActionId && x.OwnerUserId == command.UserId && x.Status == AiActionProposalStatus.Pending)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, AiActionProposalStatus.Executing), ct);
        if (claimed != 1) throw new ConflictException("ai_action_not_pending", "AI action is already being executed or is no longer pending.");
        var proposal = await GetAiActionQueryHandler.Owned(db, command.UserId, command.ActionId, ct);
        object result;
        try
        {
            result = await Execute(proposal, command.UserId, pin, command.Request.EvidenceIds ?? [], ct);
        }
        catch (AppException error)
        {
            await MarkFailedAsync(command.ActionId, error.Code);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Hủy request không được để proposal kẹt vĩnh viễn ở Executing.
            await MarkFailedAsync(command.ActionId, "AI_ACTION_CANCELLED");
            throw;
        }
        catch (Exception)
        {
            // Lỗi ngoài dự kiến không được để proposal mắc kẹt ở Executing.
            try
            {
                await MarkFailedAsync(command.ActionId, "AI_ACTION_EXECUTION_FAILED");
            }
            catch
            {
                // Giữ lỗi gốc; không ghi secret hoặc chi tiết hạ tầng vào response.
            }
            throw;
        }
        proposal.Status = AiActionProposalStatus.Executed;
        proposal.ExecutedAt = clock.UtcNow;
        db.AuditLogs.Add(new AuditLog { ActorUserId = command.UserId, Action = "AiActionExecuted", TargetType = proposal.ActionType,
            TargetId = proposal.Id.ToString(), Reason = "User confirmed an AI-prepared action.", MetadataJson = JsonSerializer.Serialize(new { proposal.Id, proposal.ActionType, Result = "Executed" }) });
        await db.SaveChangesAsync(ct);
        return GetAiActionQueryHandler.Map(proposal, result);
    }

    private async Task MarkFailedAsync(Guid proposalId, string failureCode)
    {
        // Conditional update vẫn hoạt động khi command con đã clear ChangeTracker sau rollback.
        db.ClearTrackedEntities();
        await db.AiActionProposals
            .Where(item => item.Id == proposalId && item.Status == AiActionProposalStatus.Executing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, AiActionProposalStatus.Failed)
                .SetProperty(item => item.FailureCode, failureCode), CancellationToken.None);
    }

    private async Task<object> Execute(AiActionProposal proposal, Guid userId, string pin, IReadOnlyList<Guid> evidence, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(proposal.PayloadJson);
        var p = doc.RootElement;
        string S(string name) => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new AppException("ai_action_payload_invalid", $"{name} is required.");
        Guid G(string name) => Guid.TryParse(S(name), out var id) ? id : throw new AppException("ai_action_payload_invalid", $"{name} is invalid.");
        decimal D(string name) => p.TryGetProperty(name, out var v) && v.TryGetDecimal(out var amount) ? amount : throw new AppException("ai_action_payload_invalid", $"{name} is invalid.");
        Guid? OG(string name) => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var id) ? id : null;
        decimal OD(string name, decimal fallback = 0) => p.TryGetProperty(name, out var v) && v.TryGetDecimal(out var value) ? value : fallback;
        bool OB(string name, bool fallback) => p.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : fallback;
        DateTime Date(string name) => IsoDateTimeParser.TryParseUtc(S(name), out var value)
            ? value
            : throw new AppException("ai_action_payload_invalid", $"{name} is invalid.");
        DateTime? ODate(string name) => p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? Date(name)
            : null;
        return proposal.ActionType switch
        {
            "manual_income" => await mediator.Send(new CreateIncomeCommand(userId, new CreateManualTransactionRequest(G("wallet_id"), G("category_id"), D("amount"), S("description"), null, proposal.OperationKey, pin, evidence)), ct),
            "manual_expense" => await mediator.Send(new CreateExpenseCommand(userId, new CreateManualTransactionRequest(G("wallet_id"), G("category_id"), D("amount"), S("description"), null, proposal.OperationKey, pin, evidence)), ct),
            "internal_transfer" => await mediator.Send(new TransferBetweenWalletsCommand(userId, new InternalTransferRequest(G("source_wallet_id"), G("destination_wallet_id"), D("amount"), S("description"), proposal.OperationKey, pin)), ct),
            "budget_create" => await mediator.Send(new CreateBudgetCommand(userId, new CreateBudgetRequest(G("category_id"), (int)D("year"), (int)D("month"), D("limit_amount"), OB("alerts_enabled", true), OB("rollover", false), pin)), ct),
            "budget_update" => await UpdateBudget(userId, G("budget_id"), D("limit_amount"), p, pin, ct),
            "budget_delete" => await DeleteBudget(userId, G("budget_id"), pin, ct),
            "goal_create" => await mediator.Send(new CreateGoalCommand(userId, new CreateGoalRequest(
                S("name"), D("target_amount"), ODate("target_date"), OG("category_id"), OptionalText(p, "icon_name"),
                OptionalText(p, "color_hex"), OD("initial_amount"), OG("wallet_id"), proposal.OperationKey, pin)), ct),
            "goal_update" => await UpdateGoal(userId, G("goal_id"), S("name"), D("target_amount"), p, pin, ct),
            "goal_delete" => await DeleteGoal(userId, G("goal_id"), pin, ct),
            "goal_contribution" => await mediator.Send(new ContributeGoalCommand(userId, G("goal_id"), new GoalMovementRequest(G("wallet_id"), D("amount"), proposal.OperationKey, pin)), ct),
            "goal_withdrawal" => await mediator.Send(new WithdrawGoalCommand(userId, G("goal_id"), new GoalMovementRequest(G("wallet_id"), D("amount"), proposal.OperationKey, pin)), ct),
            "recurring_occurrence_record" => await mediator.Send(new RecordRecurringOccurrenceCommand(userId, G("recurring_transaction_id"), G("occurrence_id"),
                new RecordRecurringOccurrenceRequest(pin, evidence, S("idempotency_key"))), ct),
            "transaction_correction" => await mediator.Send(new UpdateTransactionCommand(userId, G("transaction_id"),
                new UpdateTransactionRequest(G("category_id"), S("description"), pin, evidence, S("reason"), Date("occurred_at"), G("wallet_id"), D("amount"), OptionalText(p, "counterparty"))), ct),
            "bank_direct_sender_confirmation" => await mediator.Send(new ConfirmBankDirectPaymentCommand(userId, G("payment_id"), new ConfirmBankDirectPaymentRequest(pin)), ct),
            "group_create" => await mediator.Send(new CreateGroupCommand(userId, new CreateGroupRequest(S("name"), S("description"), S("type"), S("currency"), pin)), ct),
            "group_expense_create" => await mediator.Send(new CreateCurrentGroupExpenseCommand(userId, G("group_id"),
                new CreateGroupExpenseRequest(S("title"), D("amount"), G("category_id"), null, S("paid_by_type"), OG("paid_by_user_id"), Date("date"), OptionalText(p, "description"), pin, proposal.OperationKey)), ct),
            "group_deletion_vote" => await mediator.Send(new VoteGroupDeletionCommand(userId, G("group_id"), G("request_id"),
                new GroupDeletionVoteRequest(Bool(p, "approved"), pin)), ct),
            "group_refund_payment_create" => await mediator.Send(new CreateGroupRefundPaymentCommand(userId, G("group_id"), G("request_id"), G("refund_id"),
                new CreateGroupRefundPaymentRequest(S("idempotency_key"), pin)), ct),
            "group_deletion_finalize" => await mediator.Send(new FinalizeGroupDeletionCommand(userId, G("group_id"), G("request_id"),
                new FinalizeGroupDeletionRequest(pin)), ct),
            _ => throw new AppException("ai_action_unsupported", "This AI action type is not supported.", 422)
        };
    }

    private static bool Bool(JsonElement payload, string name) => payload.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        ? value.GetBoolean()
        : throw new AppException("ai_action_payload_invalid", $"{name} is invalid.");
    private static string? OptionalText(JsonElement payload, string name) => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private async Task<object> DeleteBudget(Guid userId, Guid id, string pin, CancellationToken ct)
    {
        await mediator.Send(new DeleteBudgetCommand(userId, id, pin), ct);
        return new { deleted = true, budgetId = id };
    }

    private async Task<object> UpdateBudget(Guid userId, Guid id, decimal limitAmount, JsonElement payload, string pin, CancellationToken ct)
    {
        // Khi model bỏ qua cờ tùy chọn, giữ nguyên cấu hình hiện tại thay vì âm thầm reset.
        var current = await db.Budgets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
            ?? throw new NotFoundException("budget_not_found", "Budget not found.");
        var alertsEnabled = payload.TryGetProperty("alerts_enabled", out var alerts) ? alerts.GetBoolean() : current.AlertsEnabled;
        var rollover = payload.TryGetProperty("rollover", out var rolloverValue) ? rolloverValue.GetBoolean() : current.Rollover;
        return await mediator.Send(new UpdateBudgetCommand(userId, id, new UpdateBudgetRequest(limitAmount, alertsEnabled, rollover, pin)), ct);
    }

    private async Task<object> UpdateGoal(Guid userId, Guid id, string name, decimal targetAmount, JsonElement payload, string pin, CancellationToken ct)
    {
        // Các metadata không có trong proposal được giữ nguyên theo đúng ý nghĩa partial update của AI.
        var current = await db.Goals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == userId, ct)
            ?? throw new NotFoundException("goal_not_found", "Goal not found.");
        DateTime? targetDate = payload.TryGetProperty("target_date", out var date)
            ? ParseStoredDate(date.GetString(), "target_date")
            : current.TargetDate;
        Guid? categoryId = payload.TryGetProperty("category_id", out var category) ? Guid.Parse(category.GetString()!) : current.CategoryId;
        var iconName = payload.TryGetProperty("icon_name", out var icon) ? icon.GetString() : current.IconName;
        var colorHex = payload.TryGetProperty("color_hex", out var color) ? color.GetString() : current.ColorHex;
        return await mediator.Send(new UpdateGoalCommand(userId, id,
            new UpdateGoalRequest(name, targetAmount, targetDate, categoryId, iconName, colorHex, pin)), ct);
    }

    private async Task<object> DeleteGoal(Guid userId, Guid id, string pin, CancellationToken ct)
    {
        await mediator.Send(new DeleteGoalCommand(userId, id, pin), ct);
        return new { deleted = true, goalId = id };
    }

    private static DateTime ParseStoredDate(string? value, string name) =>
        IsoDateTimeParser.TryParseUtc(value, out var parsed)
            ? parsed
            : throw new AppException("ai_action_payload_invalid", $"{name} is invalid.");
}
