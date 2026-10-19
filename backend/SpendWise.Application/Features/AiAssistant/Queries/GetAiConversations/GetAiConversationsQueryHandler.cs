using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.AI;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;
using SpendWise.Domain.Groups;

namespace SpendWise.Application.Features.AiAssistant.Queries.GetAiConversations;

// Handler lấy các cuộc hội thoại AI thuộc người dùng hiện tại.

public sealed class GetAiConversationsQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetAiConversationsQuery, IReadOnlyList<AiConversationDto>>
{
    public async Task<IReadOnlyList<AiConversationDto>> Handle(
        GetAiConversationsQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ lấy các hội thoại AI thuộc người dùng và sắp xếp theo lần cập nhật gần nhất.
        var source = db.AiConversations
            .AsNoTracking()
            .Where(conversation => conversation.UserId == query.UserId
                && conversation.ContextType != AiConversationContextType.Admin)
            // Apply the same active-group access policy as GET-by-ID before returning any history.
            .Where(conversation => conversation.ContextType != AiConversationContextType.Group
                || db.GroupMembers.Any(member => member.GroupId == conversation.ContextGroupId
                    && member.UserId == query.UserId && member.Group.Status == GroupStatus.Active));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(x => x.Title.Contains(search) || x.Messages.Any(m => m.Content.Contains(search)));
        }
        var conversations = await source
            .OrderByDescending(conversation => conversation.IsPinned)
            .ThenByDescending(conversation => conversation.UpdatedAt
                ?? conversation.CreatedAt)
            // Chỉ chọn metadata hội thoại và bốn trường cần hiển thị của mỗi tin nhắn.
            .Select(conversation => new AiConversationDto(
                conversation.Id,
                conversation.Title,
                conversation.CreatedAt,
                conversation.UpdatedAt ?? conversation.CreatedAt,
                conversation.IsPinned,
                conversation.ContextType.ToString(),
                conversation.ContextGroupId,
                conversation.Messages
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
            .ToListAsync(cancellationToken);

        return conversations;
    }
}
