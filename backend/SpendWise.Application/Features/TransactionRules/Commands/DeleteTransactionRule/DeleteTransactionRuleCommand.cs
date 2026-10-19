using MediatR;

namespace SpendWise.Application.Features.TransactionRules.Commands.DeleteTransactionRule;

// Xóa một quy tắc cấu hình thuộc người dùng hiện tại.
public sealed record DeleteTransactionRuleCommand(Guid UserId, Guid TransactionRuleId) : IRequest;
