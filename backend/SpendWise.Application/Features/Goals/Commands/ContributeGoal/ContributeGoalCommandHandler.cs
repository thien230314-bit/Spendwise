using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.ContributeGoal;

// Handler chuyển tiền từ ví vào mục tiêu bằng posting tài chính cân bằng.

public sealed class ContributeGoalCommandHandler(
    GoalMovementExecutor executor) : IRequestHandler<ContributeGoalCommand, GoalDto>
{
    public Task<GoalDto> Handle(
        ContributeGoalCommand command,
        CancellationToken cancellationToken)
    {
        // Chuyển tiền từ ví vào tài khoản dự trữ Goal bằng idempotency key.
        return executor.MoveAsync(
            command.UserId,
            command.GoalId,
            command.Request,
            true,
            cancellationToken);
    }
}
