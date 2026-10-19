using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Queries.GetTransactionRuleById;

// Trả một quy tắc phân loại thuộc người dùng cùng thông tin danh mục đích.
public sealed class GetTransactionRuleByIdQueryHandler(
    ISpendWiseDbContext db) : IRequestHandler<GetTransactionRuleByIdQuery, TransactionRuleDto>
{
    public async Task<TransactionRuleDto> Handle(
        GetTransactionRuleByIdQuery query,
        CancellationToken cancellationToken)
    {
        // Projection lấy tên danh mục đích mà không tải toàn bộ entity liên quan.
        var rule = await db.TransactionRules
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.TransactionRuleId
                && candidate.OwnerUserId == query.UserId)
            .Select(candidate => new TransactionRuleDto(
                candidate.Id,
                candidate.DescriptionPattern,
                candidate.TargetCategoryId,
                candidate.TargetCategory.Name,
                candidate.Priority,
                candidate.IsActive))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("transaction_rule_not_found", "Transaction rule not found.");

        return rule;
    }
}
