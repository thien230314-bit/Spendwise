using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class FreeFundEquityEngineTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly Guid UserC = Guid.NewGuid();

    [Fact]
    public void Example1_A600k_B400k_SharedExpense200k_Equity_480k_320k()
    {
        // A contributes 600,000 VND, B contributes 400,000 VND
        // Shared expense 200,000 VND
        // Ratio 60% / 40%: A bears 120,000 VND, B bears 80,000 VND
        // Expected: A remaining 480,000 VND, B remaining 320,000 VND, Total fund 800,000 VND.
        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, 600_000),
            new(UserB, Guid.NewGuid(), 2, 400_000)
        };

        var afterExpense = FreeFundEquityEngine.ApplySharedExpense(members, 200_000);

        Assert.Equal(480_000, afterExpense[UserA]);
        Assert.Equal(320_000, afterExpense[UserB]);
        Assert.Equal(800_000, afterExpense.Values.Sum());
    }

    [Fact]
    public void Example2_BLeavesAfterExpense_Receives320k()
    {
        // B leaves after 200k expense: refund B 320,000 VND; A retains 480,000 VND.
        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, 480_000),
            new(UserB, Guid.NewGuid(), 2, 320_000)
        };

        var refunds = FreeFundEquityEngine.CalculateRefunds(members, 800_000, [UserB]);

        Assert.Equal(320_000, refunds[UserB]);
    }

    [Fact]
    public void Example3_BHas320k_Contributes200kMore_LeavesImmediately_Receives520k()
    {
        // B has 320k, contributes 200k more, then leaves immediately without new expenses.
        // New contribution does NOT retroactively absorb past expenses.
        // Refund B = 520,000 VND.
        var equities = new Dictionary<Guid, long>
        {
            [UserA] = 480_000,
            [UserB] = 320_000
        };

        var updatedEquities = FreeFundEquityEngine.ApplyContribution(equities, UserB, 200_000);
        Assert.Equal(520_000, updatedEquities[UserB]);

        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, updatedEquities[UserA]),
            new(UserB, Guid.NewGuid(), 2, updatedEquities[UserB])
        };

        var refunds = FreeFundEquityEngine.CalculateRefunds(members, 1_000_000, [UserB]);
        Assert.Equal(520_000, refunds[UserB]);
    }

    [Fact]
    public void Example4_CJoinsAfterExpense_Contributes200k_LeavesBeforeNewExpense_ReceivesFull200k()
    {
        // A/B have total 800k after expense.
        // C newly contributes 200k, then leaves before any new expense.
        // Total fund = 1,000,000 VND. C receives full 200,000 VND. Past expenses NOT retroactively shared with C.
        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, 480_000),
            new(UserB, Guid.NewGuid(), 2, 320_000),
            new(UserC, Guid.NewGuid(), 3, 200_000)
        };

        var refunds = FreeFundEquityEngine.CalculateRefunds(members, 1_000_000, [UserC]);
        Assert.Equal(200_000, refunds[UserC]);
    }

    [Fact]
    public void Example5_BContributes400k_Withdraws100k_BeforeSharedExpense()
    {
        // B contributes 400,000 VND, withdraws 100,000 VND personal before shared expense.
        // B remaining equity = 300,000 VND. Subsequent shared expense uses 300k weight.
        var equities = new Dictionary<Guid, long>
        {
            [UserA] = 600_000,
            [UserB] = 400_000
        };

        var afterWithdrawal = FreeFundEquityEngine.ApplyPersonalWithdrawal(equities, UserB, 100_000);
        Assert.Equal(300_000, afterWithdrawal[UserB]);

        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, afterWithdrawal[UserA]),
            new(UserB, Guid.NewGuid(), 2, afterWithdrawal[UserB])
        };

        // Shared expense 180,000 VND: Total equity = 900,000 VND (A 600k / B 300k = 2/3 and 1/3)
        // A bears 120k -> remaining 480k; B bears 60k -> remaining 240k.
        var afterExpense = FreeFundEquityEngine.ApplySharedExpense(members, 180_000);
        Assert.Equal(480_000, afterExpense[UserA]);
        Assert.Equal(240_000, afterExpense[UserB]);
    }

    [Fact]
    public void Example6_BLeavesRefunded320k_RejoinsAndContributes200k_FreshBaseline()
    {
        // B leaves, refunded 320k. Rejoins later and contributes 200k.
        // Fresh episode starts with exactly 200,000 VND. Old episode does NOT carry over.
        var equities = new Dictionary<Guid, long>
        {
            [UserA] = 480_000,
            [UserB] = 0 // B starts fresh
        };

        var updated = FreeFundEquityEngine.ApplyContribution(equities, UserB, 200_000);
        Assert.Equal(200_000, updated[UserB]);

        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, 480_000),
            new(UserB, Guid.NewGuid(), 2, 200_000)
        };

        var refunds = FreeFundEquityEngine.CalculateRefunds(members, 680_000, [UserB]);
        Assert.Equal(200_000, refunds[UserB]);
    }

    [Fact]
    public void Example7_VndIntegerRounding_ConservesTotalBalance()
    {
        // 3 members with odd equities: A=333,333, B=333,333, C=333,334 (Total = 1,000,000 VND)
        // Shared expense 100,000 VND.
        // Remainder must be distributed by seat order without losing or creating 1 VND.
        var members = new List<FreeFundEquityEngine.MemberEpochState>
        {
            new(UserA, Guid.NewGuid(), 1, 333_333),
            new(UserB, Guid.NewGuid(), 2, 333_333),
            new(UserC, Guid.NewGuid(), 3, 333_334)
        };

        var afterExpense = FreeFundEquityEngine.ApplySharedExpense(members, 100_000);

        long totalAfter = afterExpense.Values.Sum();
        Assert.Equal(900_000, totalAfter);

        // On full dissolution of 900,000 VND
        var updatedMembers = members.Select(m => new FreeFundEquityEngine.MemberEpochState(m.UserId, m.MembershipId, m.Seat, afterExpense[m.UserId])).ToList();
        var dissolutionRefunds = FreeFundEquityEngine.CalculateRefunds(updatedMembers, 900_000, null);

        Assert.Equal(900_000, dissolutionRefunds.Values.Sum());
    }
}
