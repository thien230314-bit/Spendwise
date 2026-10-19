using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRules;

// Query lấy các quy tắc tự động hóa thuộc người dùng.

public sealed record GetTransactionRulesQuery(
    Guid UserId) : IRequest<IReadOnlyList<TransactionRuleDto>>;
