using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Commands.UpdateTransactionRule;

// Yêu cầu sửa một quy tắc tự động phân loại giao dịch.
public sealed record UpdateTransactionRuleCommand(
    Guid UserId,
    Guid TransactionRuleId,
    UpdateTransactionRuleRequest Request) : IRequest<TransactionRuleDto>;
