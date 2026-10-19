using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.ContributeGoal;

// Command chuyển tiền từ ví vào mục tiêu bằng posting tài chính cân bằng.

public sealed record ContributeGoalCommand(
    Guid UserId,
    Guid GoalId,
    GoalMovementRequest Request) : IRequest<GoalDto>;
