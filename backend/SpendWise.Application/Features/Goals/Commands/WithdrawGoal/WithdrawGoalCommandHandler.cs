using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Commands.WithdrawGoal;

// Handler rút tiền khỏi mục tiêu về ví bằng posting tài chính cân bằng.

public sealed class WithdrawGoalCommandHandler(
    GoalMovementExecutor executor) : IRequestHandler<WithdrawGoalCommand, GoalDto>
{
    public Task<GoalDto> Handle(
        WithdrawGoalCommand command,
        CancellationToken cancellationToken)
    {
        // Chuyển tiền từ tài khoản dự trữ Goal về đúng ví của chủ sở hữu.
        return executor.MoveAsync(
            command.UserId,
            command.GoalId,
            command.Request,
            false,
            cancellationToken);
    }
}
