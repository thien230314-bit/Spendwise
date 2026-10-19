using MediatR;
using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Common;
using SpendWise.Application.Common.Exceptions;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Application.Planning;

namespace SpendWise.Application.Features.TransactionRules.Commands.UpdateTransactionRule;

// Sửa điều kiện và danh mục đích của quy tắc thuộc người dùng hiện tại.
public sealed class UpdateTransactionRuleCommandHandler(
    ISpendWiseDbContext db) : IRequestHandler<UpdateTransactionRuleCommand, TransactionRuleDto>
{
    public async Task<TransactionRuleDto> Handle(
        UpdateTransactionRuleCommand command,
        CancellationToken cancellationToken)
    {
        // Mẫu so khớp phải đủ rõ và không vượt giới hạn cột dữ liệu.
        var pattern = TransactionTextNormalizer.Normalize(command.Request.DescriptionPattern);
        if (pattern.Length is < 2 or > 160)
        {
            throw new AppException("invalid_rule", "A pattern of 2 to 160 characters is required.");
        }

        // Chỉ chủ sở hữu mới được sửa quy tắc.
        var rule = await db.TransactionRules.SingleOrDefaultAsync(
            candidate => candidate.Id == command.TransactionRuleId
                && candidate.OwnerUserId == command.UserId,
            cancellationToken)
            ?? throw new NotFoundException("transaction_rule_not_found", "Transaction rule not found.");

        // Danh mục đích phải là danh mục hệ thống hoặc của chính người dùng và còn hoạt động.
        var category = await db.Categories.SingleOrDefaultAsync(
            candidate => candidate.Id == command.Request.TargetCategoryId
                && candidate.IsActive
                && (candidate.IsSystem || candidate.OwnerUserId == command.UserId),
            cancellationToken)
            ?? throw new AppException("invalid_category", "Category is invalid.");

        rule.DescriptionPattern = pattern;
        rule.TargetCategoryId = category.Id;
        rule.TargetCategory = category;
        rule.Priority = command.Request.Priority;
        rule.IsActive = command.Request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return TransactionRuleMappings.ToDto(rule);
    }
}
