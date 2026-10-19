using MediatR;
using SpendWise.Application.AI;

namespace SpendWise.Application.Features.AiAssistant.Commands.SendAiMessage;

// Command lọc nội dung, lưu tin nhắn và lấy phản hồi AI cho đúng cuộc hội thoại.

public sealed record SendAiMessageCommand(
    Guid UserId,
    string SystemRole,
    string Locale,
    AiChatRequest Request) : IRequest<AiChatResponse>;
