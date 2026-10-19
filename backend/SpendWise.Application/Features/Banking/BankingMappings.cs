using SpendWise.Application.Banking;
using SpendWise.Domain.Banking;

namespace SpendWise.Application.Features.Banking;

// Các mapping tập trung bảo đảm API không vô tình trả ExternalAccountId ở endpoint người dùng.
internal static class BankingMappings
{
    public static BankConnectionDto ToUserDto(BankConnection connection) => new(
        connection.Id,
        connection.Provider.ToString(),
        connection.AccountEnvironment.ToString(),
        connection.BankBrandName,
        connection.AccountType,
        connection.AccountHolderName,
        connection.MaskedAccountNumber,
        connection.Status.ToString(),
        connection.LinkedAt,
        connection.LastSyncedAt);

    public static BankTransactionDto ToTransactionDto(BankTransaction transaction) => new(
        transaction.Id,
        transaction.BankConnectionId,
        transaction.BankConnection.BankBrandName,
        transaction.BankConnection.MaskedAccountNumber,
        transaction.ExternalTransactionId,
        transaction.Direction.ToString(),
        transaction.Amount,
        transaction.Currency,
        transaction.TransactionDate,
        transaction.Content,
        transaction.ReferenceCode,
        transaction.PaymentCode,
        transaction.Gateway,
        transaction.Source.ToString(),
        transaction.ImportedAt,
        transaction.CategoryId,
        transaction.Category?.Name);

    public static AdminBankConnectionDto ToAdminDto(BankConnection connection) => new(
        connection.Id,
        connection.UserId,
        connection.Provider.ToString(),
        connection.AccountEnvironment.ToString(),
        connection.ExternalAccountId,
        connection.BankBrandName,
        connection.MaskedAccountNumber,
        connection.Status.ToString(),
        connection.LinkedAt,
        connection.LastSyncedAt,
        connection.CreatedAt);
}
