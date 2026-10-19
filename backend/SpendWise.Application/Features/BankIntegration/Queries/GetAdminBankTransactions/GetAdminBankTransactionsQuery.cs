using MediatR;
using SpendWise.Application.Banking;
using SpendWise.Application.Finance;

namespace SpendWise.Application.Features.BankIntegration.Queries.GetAdminBankTransactions;

// Query quản trị đọc giao dịch ngân hàng ngoài, không dùng đường ownership của User API.
public sealed record GetAdminBankTransactionsQuery(
    AdminBankTransactionFilter Filter) : IRequest<PagedResult<AdminBankTransactionDto>>;
