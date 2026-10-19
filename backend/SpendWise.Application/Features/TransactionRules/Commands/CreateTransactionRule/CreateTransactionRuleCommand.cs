using MediatR;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Commands.CreateTransactionRule;

// Command tạo quy tắc tự động hóa thuộc người dùng hiện tại.

public sealed record CreateTransactionRuleCommand(
    Guid UserId,
    CreateTransactionRuleRequest Request) : IRequest<TransactionRuleDto>;
