using SpendWise.Application.Finance;
using SpendWise.Domain.Finance;

namespace SpendWise.Application.Features.Wallets;

// Lớp này chuyển Wallet sang DTO mà không cho lớp API thao tác trực tiếp entity.

internal static class WalletMappings
{
    public static WalletDto ToDto(Wallet wallet)
    {
        // Tạo WalletDto từ metadata và số dư hiện tại của Wallet.
        return new WalletDto(
            wallet.Id,
            wallet.Name,
            ToApiType(wallet.Type),
            wallet.Currency,
            wallet.Status.ToString(),
            wallet.AvailableBalance,
            wallet.IsDefault,
            wallet.CreatedAt);
    }

    // Giữ enum Domain ổn định nhưng trả về tên semantic mà frontend sử dụng.
    public static string ToApiType(WalletType type) => type switch
    {
        WalletType.Main => "spending",
        WalletType.Savings => "savings",
        WalletType.Emergency => "emergency",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    // Chấp nhận cả contract mới và tên enum cũ để client cũ không bị hỏng.
    public static bool TryParseApiType(string value, out WalletType type) => value.Trim().ToLowerInvariant() switch
    {
        "spending" or "main" => Set(WalletType.Main, out type),
        "savings" => Set(WalletType.Savings, out type),
        "emergency" => Set(WalletType.Emergency, out type),
        _ => Set(default, out type, false)
    };

    private static bool Set(WalletType value, out WalletType type, bool valid = true)
    {
        type = value;
        return valid;
    }
}
