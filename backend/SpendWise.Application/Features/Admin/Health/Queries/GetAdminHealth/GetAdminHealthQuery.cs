using MediatR;

namespace SpendWise.Application.Features.Admin.Health.Queries.GetAdminHealth;

// Query kiểm tra trạng thái database và các thành phần hạ tầng.

public sealed record GetAdminHealthQuery : IRequest<object>;
