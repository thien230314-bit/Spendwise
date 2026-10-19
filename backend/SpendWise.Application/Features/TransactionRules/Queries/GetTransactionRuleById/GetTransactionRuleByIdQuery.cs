using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRuleById;

// Lấy một quy tắc phân loại theo Id và chủ sở hữu.
public sealed record GetTransactionRuleByIdQuery(
    Guid UserId,
    Guid TransactionRuleId) : IRequest<TransactionRuleDto>;
