using System.Text;

namespace SpendWise.Application.Common;

// Chuẩn hóa Unicode và chữ hoa giống nhau cho cả rule lẫn mô tả giao dịch.
public static class TransactionTextNormalizer
{
    public static string Normalize(string value) =>
        value.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
