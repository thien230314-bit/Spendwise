using SpendWise.Application.Common.Exceptions;

namespace SpendWise.Application.Features.Banking;

// Tạo exception thống nhất để mọi Bank Hub endpoint tiếp tục dùng ProblemDetails hiện có.
internal static class BankingErrors
{
    public static AppException Unavailable() => new(
        "bank_integration_unavailable",
        "Bank integration is disabled or not configured.",
        503);

    public static NotFoundException ConnectionNotFound() => new(
        "bank_connection_not_found",
        "Bank connection not found.");
}
