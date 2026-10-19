using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Queries.GetGoals;

// Handler lấy các mục tiêu tiết kiệm thuộc người dùng hiện tại.

public sealed class GetGoalsQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetGoalsQuery, IReadOnlyList<GoalDto>>
{
    public async Task<IReadOnlyList<GoalDto>> Handle(
        GetGoalsQuery query,
        CancellationToken cancellationToken)
    {
        // Chỉ lấy Goal thuộc người dùng và sắp xếp mục tiêu mới nhất trước.
        var goals = await db.Goals
            .AsNoTracking()
            .Include(goal => goal.Movements)
            .Where(goal => goal.OwnerUserId == query.UserId)
            .OrderByDescending(goal => goal.CreatedAt)
            .ToListAsync(cancellationToken);

        return goals.Select(GoalMappings.ToDto).ToList();
    }
}
