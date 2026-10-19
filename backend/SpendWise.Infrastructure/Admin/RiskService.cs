using Microsoft.EntityFrameworkCore;
using SpendWise.Application.Admin;
using SpendWise.Application.Common.Interfaces;
using SpendWise.Domain.Admin;
using SpendWise.Domain.Finance;
using SpendWise.Domain.Banking;
using SpendWise.Infrastructure.Persistence;

namespace SpendWise.Infrastructure.Admin;

// Đánh giá rủi ro giao dịch theo các ngưỡng cấu hình quản trị.

public sealed class RiskService(
    SpendWiseDbContext db,
    IOperationPolicy policy,
    IClock clock) : IRiskService
{
    public async Task<RiskAssessment> AssessTransferAsync(
        Guid userId,
        decimal amount,
        string? sessionId,
        CancellationToken ct)
    {
        // Chấm điểm rủi ro trước khi cho phép transfer làm thay đổi số dư.
        var now = clock.UtcNow;
        var dayStart = now.Date;
        var score = 0;
        var signals = new List<string>();
        var maximum = await policy.GetDecimalAsync("TransferMaximum", 50_000_000m, ct);
        var dailyLimit = await policy.GetDecimalAsync("DailyTransferLimit", 100_000_000m, ct);
        var reviewThreshold = (int)Math.Clamp(
            await policy.GetDecimalAsync("RiskReviewThreshold", 70m, ct),
            1m,
            99m);

        if (amount > maximum)
        {
            score += 100;
            signals.Add("amount_over_maximum");
        }

        var dailyTotal = await db.BankDirectPayments
            .Where(x => x.SenderUserId == userId
                && x.ContextType == BankDirectPaymentContextType.Transfer
                && x.Status == BankDirectPaymentStatus.Completed
                && x.CreatedAt >= dayStart)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        if (dailyTotal + amount > dailyLimit)
        {
            score += 100;
            signals.Add("daily_limit_exceeded");
        }

        var recentCount = await db.BankDirectPayments.CountAsync(
            x => x.SenderUserId == userId
                && x.ContextType == BankDirectPaymentContextType.Transfer
                && x.CreatedAt >= now.AddMinutes(-10),
            ct);
        if (recentCount >= 5)
        {
            score += 35;
            signals.Add("transfer_velocity");
        }

        var userCreatedAt = await db.Users
            .Where(x => x.Id == userId)
            .Select(x => x.CreatedAt)
            .SingleAsync(ct);
        if (userCreatedAt > now.AddDays(-7) && amount >= 10_000_000m)
        {
            score += 35;
            signals.Add("new_account_high_value");
        }

        var average = await db.BankDirectPayments
            .Where(x => x.SenderUserId == userId
                && x.ContextType == BankDirectPaymentContextType.Transfer
                && x.Status == BankDirectPaymentStatus.Completed)
            .AverageAsync(x => (decimal?)x.Amount, ct) ?? 0;
        if (average > 0 && amount > average * 5)
        {
            score += 25;
            signals.Add("unusual_amount");
        }

        if (!string.IsNullOrWhiteSpace(sessionId) && !Guid.TryParse(sessionId, out _))
        {
            score += 15;
            signals.Add("unrecognized_session");
        }

        var level = score >= reviewThreshold
            ? RiskLevel.High
            : score >= 35
                ? RiskLevel.Medium
                : RiskLevel.Low;
        var decision = score >= 100
            ? RiskDecision.Block
            : score >= reviewThreshold
                ? RiskDecision.Review
                : RiskDecision.Allow;
        Guid? caseId = null;

        if (decision != RiskDecision.Allow)
        {
            var riskCase = new RiskCase
            {
                UserId = userId,
                RiskScore = score,
                RiskLevel = level,
                Decision = decision,
                Signals = string.Join(',', signals)
            };
            db.RiskCases.Add(riskCase);
            await db.SaveChangesAsync(ct);
            caseId = riskCase.Id;
        }

        return new RiskAssessment(
            score,
            level.ToString(),
            decision.ToString(),
            signals,
            caseId);
    }
}
