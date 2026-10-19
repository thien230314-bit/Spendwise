using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.Goals.Commands.UpdateGoal;

// Sửa metadata mục tiêu nhưng không cho ghi trực tiếp số tiền đã phân bổ.
public sealed class UpdateGoalCommandHandler(
    ISpendWiseDbContext db,
    FinancialSecurityGuard security,
    IClock clock) : IRequestHandler<UpdateGoalCommand, GoalDto>
{
    public async Task<GoalDto> Handle(UpdateGoalCommand command, CancellationToken cancellationToken)
    {
        await security.VerifyAsync(command.UserId, command.Request.SecurityPin, cancellationToken);
        return await db.ExecuteInTransactionAsync(
            token => UpdateInTransactionAsync(command, token), cancellationToken);
    }

    private async Task<GoalDto> UpdateInTransactionAsync(UpdateGoalCommand command, CancellationToken cancellationToken)
    {
        // Tên và số tiền mục tiêu phải hợp lệ trước khi cập nhật.
        var goalName = InputBoundary.RequiredText(command.Request.Name, 120, "Goal name");
        InputBoundary.PositiveMoney(command.Request.TargetAmount, "Target amount");
        InputBoundary.FutureDate(command.Request.TargetDate, clock.UtcNow, "Target date");

        await GoalMovementExecutor.SerializeGoalAsync(db, command.UserId, command.GoalId, cancellationToken);

        // Chỉ chủ sở hữu mới được sửa mục tiêu.
        var goal = await db.Goals.Include(candidate => candidate.Movements).SingleOrDefaultAsync(
            candidate => candidate.Id == command.GoalId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("goal_not_found", "Goal not found.");

        // Không cho giảm mục tiêu xuống thấp hơn số tiền đã được phân bổ.
        if (command.Request.TargetAmount < goal.CurrentAllocatedAmount)
        {
            throw new ConflictException(
                "goal_target_below_allocated",
                "Target amount cannot be lower than the currently allocated amount.");
        }

        // CurrentAllocatedAmount chỉ thay đổi qua ContributeGoal hoặc WithdrawGoal.
        goal.Name = goalName;
        goal.TargetAmount = command.Request.TargetAmount;
        goal.TargetDate = command.Request.TargetDate?.ToUniversalTime();
        goal.IconName = InputBoundary.OptionalText(command.Request.IconName, 80, "Icon name");
        goal.ColorHex = ValidateColor(command.Request.ColorHex);
        if (command.Request.CategoryId is Guid categoryId)
        {
            var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == categoryId
                && item.IsActive && (item.IsSystem || item.OwnerUserId == command.UserId), cancellationToken)
                ?? throw new AppException("invalid_category", "Goal category is invalid.");
            goal.CategoryId = category.Id;
            goal.CategorySnapshot = category.Name;
        }
        else
        {
            goal.CategoryId = null;
            goal.CategorySnapshot = null;
        }
        // Chỉ Active và Completed tự đổi theo tiến độ; Paused/Cancelled luôn được giữ nguyên.
        if (goal.Status is GoalStatus.Active or GoalStatus.Completed)
        {
            goal.Status = goal.CurrentAllocatedAmount >= goal.TargetAmount
                ? GoalStatus.Completed
                : GoalStatus.Active;
        }
        await db.SaveChangesAsync(cancellationToken);

        return GoalMappings.ToDto(goal);
    }

    private static string? ValidateColor(string? value)
    {
        var color = InputBoundary.OptionalText(value, 20, "Color");
        if (!string.IsNullOrEmpty(color)
            && !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
        {
            throw new AppException("invalid_color", "Color must use #RRGGBB format.");
        }
        return color;
    }
}
