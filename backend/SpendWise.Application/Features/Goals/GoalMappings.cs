using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Goals;

// Lớp này chuyển Goal sang DTO và tính phần trăm tiến độ từ số tiền đã phân bổ.

internal static class GoalMappings
{
    public static GoalDto ToDto(Goal goal)
    {
        // Tính phần trăm hoàn thành từ tiền đã phân bổ trước khi tạo GoalDto.
        var percentage = goal.TargetAmount == 0
            ? 0
            : Math.Round(goal.CurrentAllocatedAmount / goal.TargetAmount * 100, 2);

        return new GoalDto(
            goal.Id,
            goal.Name,
            goal.TargetAmount,
            goal.CurrentAllocatedAmount,
            goal.TargetDate,
            goal.Status.ToString(),
            percentage,
            goal.CategoryId,
            goal.CategorySnapshot,
            goal.IconName,
            goal.ColorHex,
            goal.Movements.OrderByDescending(movement => movement.CreatedAt)
                .Select(movement => new GoalMovementDto(
                    movement.Id,
                    movement.Amount,
                    movement.CreatedAt,
                    movement.WalletId,
                    movement.WalletNameSnapshot,
                    movement.Type == GoalMovementType.Contribution ? "deposit" : "withdrawal"))
                .ToList());
    }
}
