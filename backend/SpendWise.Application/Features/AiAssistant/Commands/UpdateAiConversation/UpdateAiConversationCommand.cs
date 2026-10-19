using MediatR;
using SpendWise.Application.AI;

namespace SpendWise.Application.Features.AiAssistant.Commands.UpdateAiConversation;

// Yêu cầu đổi tiêu đề của một hội thoại AI.
public sealed record UpdateAiConversationCommand(
    Guid UserId,
    Guid ConversationId,
    UpdateAiConversationRequest Request) : IRequest<AiConversationDto>;
