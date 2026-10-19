using MediatR;

namespace SpendWise.Application.Features.Goals.Commands.DeleteGoal;

// Xóa mục tiêu rỗng chưa có lịch sử tài chính.
public sealed record DeleteGoalCommand(Guid UserId, Guid GoalId, string SecurityPin) : IRequest;
