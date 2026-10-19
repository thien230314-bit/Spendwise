using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.Goals.Queries.GetGoalById;

// Lấy chi tiết một mục tiêu thuộc người dùng hiện tại.
public sealed record GetGoalByIdQuery(Guid UserId, Guid GoalId) : IRequest<GoalDto>;
