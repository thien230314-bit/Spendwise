using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.AI;

namespace SpendWise.Application.Features.AiAssistant.Commands.DeleteAiConversation;

// Handler xóa cuộc hội thoại AI chỉ khi nó thuộc người dùng hiện tại.

public sealed class DeleteAiConversationCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<DeleteAiConversationCommand>
{
    public async Task Handle(
        DeleteAiConversationCommand command,
        CancellationToken cancellationToken)
    {
        // Chỉ xóa hội thoại và tin nhắn thuộc người dùng hiện tại.
        var conversation = await db.AiConversations
            .Include(item => item.Messages)
            .Include(item => item.Attachments)
            .Include(item => item.ActionProposals)
            .SingleOrDefaultAsync(
                item => item.Id == command.ConversationId
                    && item.UserId == command.UserId
                    && item.ContextType != AiConversationContextType.Admin,
                cancellationToken)
            ?? throw new NotFoundException(
                "conversation_not_found",
                "Conversation not found.");

        db.AiAttachments.RemoveRange(conversation.Attachments);
        db.AiActionProposals.RemoveRange(conversation.ActionProposals);
        db.AiMessages.RemoveRange(conversation.Messages);
        db.AiConversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
    }
}
