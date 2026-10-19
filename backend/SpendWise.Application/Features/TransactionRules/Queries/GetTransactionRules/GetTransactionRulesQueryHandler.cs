using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRules;

// Handler lấy các quy tắc tự động hóa thuộc người dùng.

public sealed class GetTransactionRulesQueryHandler(
    ISpendWiseDbContext db)
    : IRequestHandler<GetTransactionRulesQuery, IReadOnlyList<TransactionRuleDto>>
{
    public async Task<IReadOnlyList<TransactionRuleDto>> Handle(
        GetTransactionRulesQuery query,
        CancellationToken cancellationToken)
    {
        // Lấy quy tắc phân loại của người dùng cùng tên danh mục đích.
        var rules = await db.TransactionRules
            .AsNoTracking()
            .Where(rule => rule.OwnerUserId == query.UserId)
            .OrderByDescending(rule => rule.Priority)
            // Navigation chỉ được dùng trong projection nên EF tự tạo JOIN tối thiểu.
            .Select(rule => new TransactionRuleDto(
                rule.Id,
                rule.DescriptionPattern,
                rule.TargetCategoryId,
                rule.TargetCategory.Name,
                rule.Priority,
                rule.IsActive))
            .ToListAsync(cancellationToken);

        return rules;
    }
}
