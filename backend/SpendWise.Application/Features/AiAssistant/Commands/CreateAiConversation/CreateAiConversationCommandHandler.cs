using MediatR;
using SpendWise.Application.AI;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;
using SpendWise.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant.Commands.CreateAiConversation;

// Handler tạo cuộc hội thoại AI mới thuộc người dùng hiện tại.

public sealed class CreateAiConversationCommandHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<CreateAiConversationCommand, AiConversationDto>
{
    public async Task<AiConversationDto> Handle(
        CreateAiConversationCommand command,
        CancellationToken cancellationToken)
    {
        // Chuẩn hóa tiêu đề và gắn hội thoại mới với người dùng hiện tại.
        var title = string.IsNullOrWhiteSpace(command.Request.Title)
            ? "New conversation"
            : InputBoundary.RequiredText(command.Request.Title, 160, "Conversation title");
        if (!Enum.TryParse<AiConversationContextType>(command.Request.ContextType, true, out var contextType))
            throw new AppException("invalid_ai_context", "AI conversation context is invalid.");
        if (contextType == AiConversationContextType.Admin)
            throw new AppException("invalid_ai_context", "AI conversation context is invalid.");
        if (contextType == AiConversationContextType.Group)
        {
            if (command.Request.ContextGroupId is null)
                throw new AppException("group_context_required", "Group context requires a group id.");
            var member = await db.GroupMembers.AnyAsync(x => x.GroupId == command.Request.ContextGroupId
                && x.UserId == command.UserId && x.Group.Status == GroupStatus.Active, cancellationToken);
            if (!member) throw new ForbiddenException("group_membership_required", "Active group membership is required.");
        }
        var conversation = new AiConversation
        {
            UserId = command.UserId,
            Title = title,
            ContextType = contextType,
            ContextGroupId = contextType == AiConversationContextType.Group ? command.Request.ContextGroupId : null
        };
        db.AiConversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return AiConversationMappings.ToDto(conversation);
    }
}
