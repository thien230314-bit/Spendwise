namespace SpendWise.Domain.V1Groups;

/// <summary>
/// Domain model and calculation engine for Free Fund V1 Dynamic Time-Weighted Equity.
/// Follows specifications in User.md (sections 1.3, 1.3.1, and 3.6 item 10).
/// Invariants:
/// - Balance = sum of active members' equity.
/// - Integer VND arithmetic with remainder distribution ordered by occupied seat.
/// - Historical epochs: each contribution or settled shared expense or personal withdrawal updates equity.
/// - Newcomers or new episodes begin with 0 past share and do not retroactively absorb past expenses.
/// - Never silently alter or clamp event amounts; if historical data invalid, fail audit.
/// </summary>
public static class FreeFundEquityEngine
{
    public sealed record MemberEpochState(Guid UserId, Guid MembershipId, int Seat, long EquityVnd);

    /// <summary>
    /// Applies a direct contribution into the contributor's current membership episode equity.
    /// </summary>
    public static Dictionary<Guid, long> ApplyContribution(
        IReadOnlyDictionary<Guid, long> currentEquities,
        Guid contributorUserId,
        long amountVnd)
    {
        if (amountVnd <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountVnd), "Contribution amount must be positive.");

        var updated = new Dictionary<Guid, long>(currentEquities);
        var current = updated.GetValueOrDefault(contributorUserId, 0L);
        updated[contributorUserId] = current + amountVnd;
        return updated;
    }

    /// <summary>
    /// Applies a personal withdrawal from the requester's own accumulated equity.
    /// Invariant: Withdrawal cannot exceed the member's current equity.
    /// </summary>
    public static Dictionary<Guid, long> ApplyPersonalWithdrawal(
        IReadOnlyDictionary<Guid, long> currentEquities,
        Guid requesterUserId,
        long amountVnd)
    {
        if (amountVnd <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountVnd), "Withdrawal amount must be positive.");

        var updated = new Dictionary<Guid, long>(currentEquities);
        var current = updated.GetValueOrDefault(requesterUserId, 0L);
        if (amountVnd > current)
            throw new InvalidOperationException($"Withdrawal amount {amountVnd} exceeds member equity {current}.");

        updated[requesterUserId] = current - amountVnd;
        return updated;
    }

    /// <summary>
    /// Distributes a general shared expense proportionally based on each active member's equity right before the expense.
    /// Any VND remainder is distributed by occupied seat order (lowest seat index first).
    /// Invariant: Expense amount cannot exceed total positive equity.
    /// </summary>
    public static Dictionary<Guid, long> ApplySharedExpense(
        IReadOnlyList<MemberEpochState> activeMembers,
        long expenseAmountVnd)
    {
        if (expenseAmountVnd <= 0)
            throw new ArgumentOutOfRangeException(nameof(expenseAmountVnd), "Expense amount must be positive.");

        var orderedMembers = activeMembers.OrderBy(m => m.Seat).ToList();
        var positiveMembers = orderedMembers.Where(m => m.EquityVnd > 0).ToList();
        long totalPositiveEquity = positiveMembers.Sum(m => m.EquityVnd);

        if (expenseAmountVnd > totalPositiveEquity)
            throw new InvalidOperationException($"Shared expense {expenseAmountVnd} exceeds total positive equity {totalPositiveEquity}.");

        // Calculate proportional charge per member with integer division
        var charges = new Dictionary<Guid, long>();
        foreach (var m in orderedMembers)
        {
            charges[m.UserId] = 0;
        }

        if (totalPositiveEquity > 0)
        {
            long allocatedSum = 0;
            foreach (var m in positiveMembers)
            {
                long share = (long)Math.Floor((decimal)expenseAmountVnd * m.EquityVnd / totalPositiveEquity);
                charges[m.UserId] = share;
                allocatedSum += share;
            }

            long remainder = expenseAmountVnd - allocatedSum;
            for (int i = 0; i < remainder && i < positiveMembers.Count; i++)
            {
                charges[positiveMembers[i].UserId] += 1;
            }
        }

        // Apply charges to deduct from equities
        var updated = new Dictionary<Guid, long>();
        foreach (var m in orderedMembers)
        {
            long charged = charges[m.UserId];
            updated[m.UserId] = m.EquityVnd - charged;
        }

        return updated;
    }

    /// <summary>
    /// Computes refund entitlement for a single member leaving or being kicked, or for full dissolution.
    /// Invariant: Total active member equity MUST strictly equal currentFundBalanceVnd.
    /// If there is ANY discrepancy, throws InvalidOperationException to halt settlement.
    /// Refund is strictly the exact calculated equity without silent clamping or truncation.
    /// </summary>
    public static Dictionary<Guid, long> CalculateRefunds(
        IReadOnlyList<MemberEpochState> activeMembers,
        long currentFundBalanceVnd,
        IReadOnlyList<Guid>? singleMemberUserIds = null)
    {
        var orderedMembers = activeMembers.OrderBy(m => m.Seat).ToList();
        var results = new Dictionary<Guid, long>();

        long sumEquity = orderedMembers.Sum(m => m.EquityVnd);
        if (sumEquity != currentFundBalanceVnd)
        {
            throw new InvalidOperationException(
                $"Free Fund equity reconciliation mismatch: Total active member equity ({sumEquity} VND) does not match treasury balance ({currentFundBalanceVnd} VND). Reconciliation required.");
        }

        if (singleMemberUserIds is not null)
        {
            // Single member preview / leave / kick: refund is exactly their current equity
            foreach (var targetUserId in singleMemberUserIds)
            {
                var member = orderedMembers.FirstOrDefault(m => m.UserId == targetUserId);
                results[targetUserId] = member?.EquityVnd ?? 0L;
            }
            return results;
        }
        else
        {
            // Full dissolution: each active member receives their exact equity
            foreach (var m in orderedMembers)
            {
                results[m.UserId] = m.EquityVnd;
            }
            return results;
        }
    }
}
