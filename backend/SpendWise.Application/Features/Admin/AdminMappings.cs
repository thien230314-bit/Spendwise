using SpendWise.Application.Admin;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Identity;

namespace SpendWise.Application.Features.Admin;

// Lớp này chuyển User và các hồ sơ quản trị sang DTO chỉ đọc.

internal static class AdminMappings
{
    public static AdminUserDto ToDto(User user)
    {
        // Chuyển thông tin tài khoản cần cho quản trị sang AdminUserDto.
        return new AdminUserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.SpendId,
            user.Status.ToString(),
            user.SystemRole.ToString(),
            user.MustChangePassword,
            !string.IsNullOrWhiteSpace(user.SecurityPinHash),
            user.CreatedAt,
            user.LastLoginAt);
    }

    public static CaseDto ToRiskCaseDto(RiskCase item)
    {
        // Chuyển RiskCase thành CaseDto chỉ đọc cho màn hình review.
        return new CaseDto(
            item.Id,
            "Risk",
            item.ReviewStatus.ToString(),
            item.Signals,
            item.UserId,
            item.TransactionId,
            item.CreatedAt);
    }

    public static CaseDto ToDisputeDto(Dispute item)
    {
        // Chuyển Dispute thành CaseDto mà không cho sửa giao dịch liên kết.
        return new CaseDto(
            item.Id,
            "Dispute",
            item.Status.ToString(),
            item.Reason,
            item.UserId,
            item.LinkedTransactionId,
            item.CreatedAt);
    }

    public static CaseDto ToSupportCaseDto(SupportCase item)
    {
        // Chuyển SupportCase thành CaseDto phục vụ workflow hỗ trợ.
        return new CaseDto(
            item.Id,
            "Support",
            item.Status.ToString(),
            item.Category,
            item.LinkedUserId,
            item.LinkedTransactionId,
            item.CreatedAt);
    }
}
