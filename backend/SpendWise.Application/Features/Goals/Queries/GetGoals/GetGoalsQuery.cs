using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Queries.GetGoals;

// Query lấy các mục tiêu tiết kiệm thuộc người dùng hiện tại.

public sealed record GetGoalsQuery(Guid UserId) : IRequest<IReadOnlyList<GoalDto>>;
