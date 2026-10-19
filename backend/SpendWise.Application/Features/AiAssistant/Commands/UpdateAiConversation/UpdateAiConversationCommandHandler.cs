using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant.Commands.UpdateAiConversation;

// Chỉ đổi tiêu đề hội thoại thuộc người dùng, không sửa nội dung tin nhắn.
public sealed class UpdateAiConversationCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<UpdateAiConversationCommand, AiConversationDto>
{
    public async Task<AiConversationDto> Handle(
        UpdateAiConversationCommand command,
        CancellationToken cancellationToken)
    {
        // Authorize the current context before loading history or changing its context.
        var access = await db.AiConversations.AsNoTracking()
            .Where(candidate => candidate.Id == command.ConversationId && candidate.UserId == command.UserId
                && candidate.ContextType != AiConversationContextType.Admin)
            .Select(candidate => new { candidate.ContextType, candidate.ContextGroupId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("conversation_not_found", "Conversation not found.");
        if (access.ContextType == AiConversationContextType.Group)
        {
            var active = await db.GroupMembers.AnyAsync(member => member.GroupId == access.ContextGroupId
                && member.UserId == command.UserId && member.Group.Status == GroupStatus.Active, cancellationToken);
            if (!active) throw new ForbiddenException("group_membership_required", "Active group membership is required.");
        }

        // Chỉ chủ sở hữu mới được đổi tiêu đề hội thoại.
        var conversation = await db.AiConversations
            .Include(candidate => candidate.Messages)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.ConversationId
                    && candidate.UserId == command.UserId
                    && candidate.ContextType != AiConversationContextType.Admin,
                cancellationToken)
            ?? throw new NotFoundException("conversation_not_found", "Conversation not found.");

        var contextType = command.Request.ContextType is null
            ? conversation.ContextType
            : Enum.TryParse<AiConversationContextType>(command.Request.ContextType, true, out var parsed)
                ? parsed
                : throw new AppException("invalid_ai_context", "AI conversation context is invalid.");
        if (contextType == AiConversationContextType.Admin)
            throw new AppException("invalid_ai_context", "AI conversation context is invalid.");
        var contextGroupId = contextType == AiConversationContextType.Group
            ? command.Request.ContextGroupId ?? conversation.ContextGroupId
            : null;
        if (contextType == AiConversationContextType.Group)
        {
            if (contextGroupId is null) throw new AppException("group_context_required", "Group context requires a group id.");
            var member = await db.GroupMembers.AnyAsync(x => x.GroupId == contextGroupId && x.UserId == command.UserId
                && x.Group.Status == GroupStatus.Active, cancellationToken);
            if (!member) throw new ForbiddenException("group_membership_required", "Active group membership is required.");
        }

        if (command.Request.Title is not null)
            conversation.Title = InputBoundary.RequiredText(command.Request.Title, 160, "Conversation title");
        conversation.IsPinned = command.Request.IsPinned ?? conversation.IsPinned;
        conversation.ContextType = contextType;
        conversation.ContextGroupId = contextGroupId;
        await db.SaveChangesAsync(cancellationToken);

        return AiConversationMappings.ToDto(conversation);
    }
}
