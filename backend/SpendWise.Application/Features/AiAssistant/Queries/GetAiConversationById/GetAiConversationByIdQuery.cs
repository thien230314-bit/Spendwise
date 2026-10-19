using MediatR;
using SpendWise.Application.AI;

namespace SpendWise.Application.Features.AiAssistant.Queries.GetAiConversationById;

// Lấy một hội thoại AI cùng các tin nhắn theo quyền sở hữu.
public sealed record GetAiConversationByIdQuery(
    Guid UserId,
    Guid ConversationId) : IRequest<AiConversationDto>;
