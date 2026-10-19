using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.CreateGoal;

// Command tạo mục tiêu tiết kiệm với số tiền đích và thời hạn hợp lệ.

public sealed record CreateGoalCommand(
    Guid UserId,
    CreateGoalRequest Request) : IRequest<GoalDto>;
