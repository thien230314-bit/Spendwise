using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant.Queries.GetAiConversationById;

// Trả hội thoại và lịch sử tin nhắn khi nó thuộc người dùng đang đăng nhập.
public sealed class GetAiConversationByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetAiConversationByIdQuery, AiConversationDto>
{
    public async Task<AiConversationDto> Handle(
        GetAiConversationByIdQuery query,
        CancellationToken cancellationToken)
    {
        var access = await db.AiConversations.AsNoTracking()
            .Where(x => x.Id == query.ConversationId && x.UserId == query.UserId
                && x.ContextType != AiConversationContextType.Admin)
            .Select(x => new { x.ContextType, x.ContextGroupId }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("conversation_not_found", "Conversation not found.");
        if (access.ContextType == AiConversationContextType.Group)
        {
            var active = await db.GroupMembers.AnyAsync(x => x.GroupId == access.ContextGroupId && x.UserId == query.UserId
                && x.Group.Status == GroupStatus.Active, cancellationToken);
            if (!active) throw new ForbiddenException("group_membership_required", "Active group membership is required.");
        }
        // Projection lấy đúng lịch sử tin nhắn cần hiển thị mà không materialize entity graph.
        var conversation = await db.AiConversations
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.ConversationId
                && candidate.UserId == query.UserId
                && candidate.ContextType != AiConversationContextType.Admin)
            .Select(candidate => new AiConversationDto(
                candidate.Id,
                candidate.Title,
                candidate.CreatedAt,
                candidate.UpdatedAt ?? candidate.CreatedAt,
                candidate.IsPinned,
                candidate.ContextType.ToString(),
                candidate.ContextGroupId,
                candidate.Messages
                    .OrderBy(message => message.CreatedAt)
                    .Select(message => new AiMessageDto(
                        message.Id,
                        message.Role.ToString(),
                        message.Content,
                        message.CreatedAt,
                        message.Attachments.Select(attachment => new AiAttachmentDto(
                            attachment.Id, attachment.OriginalFileName, attachment.ContentType,
                            attachment.Length, attachment.ContentHash, attachment.CreatedAt)).ToList()))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("conversation_not_found", "Conversation not found.");

        return conversation;
    }
}
