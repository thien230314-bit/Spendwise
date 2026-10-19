using MediatR;

namespace SpendWise.Application.Features.AiAssistant.Commands.DeleteAiConversation;

// Command xóa cuộc hội thoại AI chỉ khi nó thuộc người dùng hiện tại.

public sealed record DeleteAiConversationCommand(
    Guid UserId,
    Guid ConversationId) : IRequest;
