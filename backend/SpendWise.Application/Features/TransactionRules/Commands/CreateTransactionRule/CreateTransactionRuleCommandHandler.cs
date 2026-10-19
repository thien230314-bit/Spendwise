using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;
using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.TransactionRules.Commands.CreateTransactionRule;

// Handler tạo quy tắc tự động hóa thuộc người dùng hiện tại.

public sealed class CreateTransactionRuleCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<CreateTransactionRuleCommand, TransactionRuleDto>
{
    public async Task<TransactionRuleDto> Handle(
        CreateTransactionRuleCommand command,
        CancellationToken cancellationToken)
    {
        // Xác nhận mẫu so khớp và danh mục đích trước khi tạo quy tắc phân loại.
        var pattern = string.IsNullOrWhiteSpace(command.Request.DescriptionPattern)
            ? string.Empty
            : TransactionTextNormalizer.Normalize(command.Request.DescriptionPattern);
        if (pattern.Length is < 2 or > 160)
        {
            throw new AppException(
                "invalid_rule",
                "A pattern of at least two characters is required.");
        }

        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == command.Request.TargetCategoryId
                && candidate.IsActive
                && (candidate.IsSystem || candidate.OwnerUserId == command.UserId),
            cancellationToken)
            ?? throw new AppException("invalid_category", "Category is invalid.");
        var rule = new TransactionRule
        {
            OwnerUserId = command.UserId,
            DescriptionPattern = pattern,
            TargetCategoryId = category.Id,
            Priority = command.Request.Priority
        };
        db.TransactionRules.Add(rule);
        await db.SaveChangesAsync(cancellationToken);

        rule.TargetCategory = category;
        return TransactionRuleMappings.ToDto(rule);
    }
}
