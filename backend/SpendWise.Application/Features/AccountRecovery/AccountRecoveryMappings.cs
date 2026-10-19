using SpendWise.Application.AccountRecovery;
using SpendWise.Domain.Admin;

namespace SpendWise.Application.Features.AccountRecovery;

internal static class AccountRecoveryMappings
{
    public static AccountRecoveryDto ToDto(AccountRecoveryRequest request) => new(
        request.Id,
        request.FullName,
        request.Phone,
        request.Description,
        request.EmailHint,
        request.Status.ToString(),
        request.ResolvedUserId,
        request.ReviewedByAdminId,
        request.ReviewedAt,
        request.AdminNote,
        request.CreatedAt,
        request.UpdatedAt,
        request.Reviews
            .OrderBy(item => item.CreatedAt)
            .Select(item => new AccountRecoveryReviewDto(
                item.Id,
                item.AdminId,
                item.Action,
                item.Note,
                item.CreatedAt))
            .ToList());
}
