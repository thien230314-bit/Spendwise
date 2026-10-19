using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Queries.GetGoalById;

// Trả mục tiêu tiết kiệm khi GoalId và OwnerUserId cùng khớp người gọi.
public sealed class GetGoalByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetGoalByIdQuery, GoalDto>
{
    public async Task<GoalDto> Handle(GetGoalByIdQuery query, CancellationToken cancellationToken)
    {
        // Điều kiện OwnerUserId ngăn người dùng xem mục tiêu của tài khoản khác.
        var goal = await db.Goals
            .AsNoTracking()
            .Include(candidate => candidate.Movements)
            .SingleOrDefaultAsync(
                candidate => candidate.Id == query.GoalId
                    && candidate.OwnerUserId == query.UserId,
                cancellationToken)
            ?? throw new NotFoundException("goal_not_found", "Goal not found.");

        return GoalMappings.ToDto(goal);
    }
}
