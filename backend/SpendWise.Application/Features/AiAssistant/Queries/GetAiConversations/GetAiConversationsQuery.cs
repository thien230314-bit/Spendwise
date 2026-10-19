using MediatR;
using SpendWise.Application.AI;

namespace SpendWise.Application.Features.AiAssistant.Queries.GetAiConversations;

// Query lấy các cuộc hội thoại AI thuộc người dùng hiện tại.

public sealed record GetAiConversationsQuery(
    Guid UserId,
    string? Search = null) : IRequest<IReadOnlyList<AiConversationDto>>;
