using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Admin.Transactions.Commands.SearchAdminTransactions;

// DTO giữ đúng kiểu enum và định danh của FinancialTransaction cho OpenAPI.
public sealed record AdminTransactionDto(
    Guid Id,
    FinancialTransactionType Type,
    FinancialTransactionStatus Status,
    decimal Amount,
    string Currency,
    Guid LedgerTransactionId,
    Guid? RelatedBusinessId,
    DateTime CreatedAt);
