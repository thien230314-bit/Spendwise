namespace SpendWise.Application.Banking;

// Chỉ trả dữ liệu tài khoản Test Mode cần cho Admin kiểm tra kết nối.
public sealed record SePayTestModeAccountDto(
    Guid ProviderAccountId,
    string BankName,
    string MaskedAccountNumber,
    string AccountType,
    string Status,
    decimal AccumulatedBalance);

// Dữ liệu giao dịch đã được chuẩn hóa và không chứa số tài khoản đầy đủ.
public sealed record SePayTestModeTransactionDto(
    Guid ProviderTransactionId,
    Guid ProviderAccountId,
    string Direction,
    decimal AmountIn,
    decimal AmountOut,
    decimal AccumulatedBalance,
    string Content,
    string? ReferenceNumber,
    string? Code,
    DateTime TransactionAt);

public interface ISePayTestModeAccountReader
{
    Task<IReadOnlyList<SePayTestModeAccountDto>> GetAccountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SePayTestModeTransactionDto>> GetTransactionsAsync(
        string reference,
        CancellationToken cancellationToken);
}
