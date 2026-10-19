using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.UpdateGoal;

// Yêu cầu sửa thông tin mô tả của mục tiêu, không trực tiếp sửa số tiền đã phân bổ.
public sealed record UpdateGoalCommand(
    Guid UserId,
    Guid GoalId,
    UpdateGoalRequest Request) : IRequest<GoalDto>;
