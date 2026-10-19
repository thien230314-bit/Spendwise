using SpendWise.Application.AI;
using SpendWise.Domain.AI;

namespace SpendWise.Application.Features.AiAssistant;

// Lớp này chuyển hội thoại và các message được phép sang DTO.

internal static class AiConversationMappings
{
    public static AiConversationDto ToDto(AiConversation conversation)
    {
        // Sắp xếp message theo thời gian rồi tạo DTO hội thoại cho đúng chủ sở hữu.
        var messages = conversation.Messages
            .OrderBy(message => message.CreatedAt)
            .Select(message => new AiMessageDto(
                message.Id,
                message.Role.ToString(),
                message.Content,
                message.CreatedAt,
                message.Attachments.Select(ToAttachmentDto).ToList()))
            .ToList();

        return new AiConversationDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt ?? conversation.CreatedAt,
            conversation.IsPinned,
            conversation.ContextType.ToString(),
            conversation.ContextGroupId,
            messages);
    }

    public static AiAttachmentDto ToAttachmentDto(AiAttachment attachment) => new(
        attachment.Id,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.Length,
        attachment.ContentHash,
        attachment.CreatedAt);

    public static string Truncate(string value, int maximumLength)
    {
        // Chuẩn hóa tiêu đề để không vượt giới hạn cột trong database.
        return value[..Math.Min(value.Length, maximumLength)];
    }
}
