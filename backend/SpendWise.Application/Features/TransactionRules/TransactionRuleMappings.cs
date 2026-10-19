using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.TransactionRules;

// Lớp này chuyển TransactionRule sang cấu hình trả về cho người sở hữu.

internal static class TransactionRuleMappings
{
    public static TransactionRuleDto ToDto(TransactionRule rule)
    {
        // Chuyển điều kiện, hành động và độ ưu tiên của TransactionRule sang DTO.
        return new TransactionRuleDto(
            rule.Id,
            rule.DescriptionPattern,
            rule.TargetCategoryId,
            rule.TargetCategory.Name,
            rule.Priority,
            rule.IsActive);
    }
}
