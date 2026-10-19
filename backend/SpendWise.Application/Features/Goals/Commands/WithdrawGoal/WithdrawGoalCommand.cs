using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.WithdrawGoal;

// Command rút tiền khỏi mục tiêu về ví bằng posting tài chính cân bằng.

public sealed record WithdrawGoalCommand(
    Guid UserId,
    Guid GoalId,
    GoalMovementRequest Request) : IRequest<GoalDto>;
