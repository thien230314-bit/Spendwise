using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;

namespace SpendWise.Application.Features.AiAssistant.Commands.SendAiMessage;

// Handler lọc nội dung, lưu tin nhắn và lấy phản hồi AI cho đúng cuộc hội thoại.

public sealed class SendAiMessageCommandHandler(
    ISpendWiseDbContext db,
    IAiDataTools tools,
    IProfanityFilter filter,
    IAiCompletionProvider provider,
    IAiAttachmentReader attachmentReader,
    IAiToolRegistry toolRegistry,
    IOperationPolicy policy,
    IClock clock) : IRequestHandler<SendAiMessageCommand, AiChatResponse>
{
    private const int HistoryMessageLimit = 8;

    public async Task<AiChatResponse> Handle(
        SendAiMessageCommand command,
        CancellationToken cancellationToken)
    {
        // Kiểm tra nội dung, quyền ngữ cảnh và lịch sử trước khi gọi AI provider.
        await policy.EnsureFeatureEnabledAsync("AIAssistant", cancellationToken);
        var input = InputBoundary.RequiredText(command.Request.Message, 8000, "Message");
        // Chuỗi giống credential không được lưu vào conversation hoặc gửi sang provider.
        if (Regex.IsMatch(input, @"^\s*\d{6}\s*$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(input, @"(?i)\b(password|security\s*pin|refresh\s*token|api\s*key|connection\s*string)\s*[:=]", RegexOptions.CultureInvariant))
        {
            throw new AppException(
                "ai_credential_input_rejected",
                "Use the secure confirmation screen or endpoint for PIN and credential values.",
                422);
        }
        var safeInput = filter.Evaluate(input);
        if (safeInput.Action == ContentAction.Reject)
        {
            throw new AppException(
                "content_rejected",
                IsVietnamese(command.Locale)
                    ? "Nội dung không phù hợp."
                    : "Content was rejected.");
        }

        var conversation = await GetOrCreateConversationAsync(
            command.UserId,
            command.Request,
            safeInput.SafeText,
            cancellationToken);
        var recentHistory = conversation.Messages
            .OrderBy(message => message.CreatedAt)
            .TakeLast(HistoryMessageLimit)
            .Select(message => new AiPromptMessage(
                message.Role.ToString(),
                message.Content))
            .ToList();
        var groupId = conversation.ContextType == AiConversationContextType.Group
            ? conversation.ContextGroupId
            : command.Request.GroupId;
        var context = await tools.BuildContextAsync(
            command.UserId,
            groupId,
            cancellationToken);
        var attachmentIds = command.Request.AttachmentIds ?? [];
        var providerAttachments = await attachmentReader.ReadAsync(command.UserId, attachmentIds, cancellationToken);
        var userMessage = new AiMessage
        {
            AiConversationId = conversation.Id,
            Conversation = conversation,
            Role = AiMessageRole.User,
            Content = safeInput.SafeText
        };
        if (attachmentIds.Count > 0)
        {
            var attachments = await db.AiAttachments.Where(x => attachmentIds.Contains(x.Id) && x.OwnerUserId == command.UserId).ToListAsync(cancellationToken);
            if (attachments.Any(x => x.AiMessageId != null || x.AiConversationId != null))
                throw new ConflictException("ai_attachment_already_used", "An AI attachment has already been used.");
            foreach (var attachment in attachments)
            {
                attachment.Conversation = conversation;
                attachment.Message = userMessage;
            }
        }
        db.AiMessages.Add(userMessage);

        // Lưu conversation/message trước tool loop để proposal không SaveChanges chung state chưa insert.
        // Việc này cũng giữ lại câu hỏi đã chấp nhận nếu provider tạm thời lỗi.
        await db.SaveChangesAsync(cancellationToken);

        var watch = Stopwatch.StartNew();
        var sensitiveProposalCreated = 0;
        async Task<string> ExecuteToolAsync(AiToolCall call, CancellationToken ct)
        {
            // Mỗi tin nhắn chỉ được tạo tối đa một proposal cần xác nhận bảo mật.
            if (call.Name.StartsWith("prepare_", StringComparison.Ordinal)
                && Interlocked.CompareExchange(ref sensitiveProposalCreated, 1, 0) != 0)
            {
                return JsonSerializer.Serialize(new
                {
                    error = "one_sensitive_proposal_per_turn",
                    message = "Only one sensitive action can be prepared per user message."
                });
            }

            return await toolRegistry.ExecuteAsync(command.UserId, conversation.Id, call, ct);
        }
        var completion = await provider.CompleteAsync(new AiProviderRequest(
            command.SystemRole,
            command.Locale,
            context,
            recentHistory,
            safeInput.SafeText,
            providerAttachments,
            toolRegistry.Definitions,
            ExecuteToolAsync), cancellationToken);

        var safeOutput = filter.Evaluate(completion.Message);
        var answer = safeOutput.Action == ContentAction.Reject
            ? IsVietnamese(command.Locale)
                ? "Tôi không thể cung cấp phản hồi đó."
                : "I cannot provide that response."
            : safeOutput.SafeText;

        db.AiMessages.Add(new AiMessage
        {
            AiConversationId = conversation.Id,
            Conversation = conversation,
            Role = AiMessageRole.Assistant,
            Content = answer,
            Provider = completion.Provider,
            LatencyMs = (int)watch.ElapsedMilliseconds
        });
        conversation.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new AiChatResponse(
            conversation.Id,
            answer,
            completion.Provider,
            completion.IsFallback,
            completion.ProposedAction);
    }

    private async Task<AiConversation> GetOrCreateConversationAsync(
        Guid userId,
        AiChatRequest request,
        string safeText,
        CancellationToken cancellationToken)
    {
        // Tải hội thoại thuộc người dùng hoặc tạo mới khi request chưa có ConversationId.
        if (request.ConversationId is null)
        {
            var requestedGroupId = request.GroupId;
            var contextType = requestedGroupId is null ? AiConversationContextType.General : AiConversationContextType.Group;
            var conversation = new AiConversation
            {
                UserId = userId,
                Title = AiConversationMappings.Truncate(safeText, 80),
                ContextType = contextType,
                ContextGroupId = requestedGroupId
            };
            db.AiConversations.Add(conversation);
            return conversation;
        }

        return await db.AiConversations
            .Include(conversation => conversation.Messages)
                .ThenInclude(message => message.Attachments)
            .SingleOrDefaultAsync(
                conversation => conversation.Id == request.ConversationId
                    && conversation.UserId == userId
                    && conversation.ContextType != AiConversationContextType.Admin,
                cancellationToken)
            ?? throw new NotFoundException(
                "conversation_not_found",
                "Conversation not found.");
    }

    private static bool IsVietnamese(string locale)
    {
        // Chọn ngôn ngữ phản hồi dựa trên locale của người dùng.
        return locale.StartsWith("vi", StringComparison.OrdinalIgnoreCase);
    }
}
