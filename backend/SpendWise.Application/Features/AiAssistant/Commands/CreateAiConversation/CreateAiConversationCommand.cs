using MediatR;
using SpendWise.Application.AI;

namespace SpendWise.Application.Features.AiAssistant.Commands.CreateAiConversation;

// Command tạo cuộc hội thoại AI mới thuộc người dùng hiện tại.

public sealed record CreateAiConversationCommand(
    Guid UserId,
    CreateAiConversationRequest Request) : IRequest<AiConversationDto>;
