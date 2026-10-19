using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FixedAllocationTests
{
    [Theory]
    [InlineData(10_000_000, 19, 526316, 15)]
    [InlineData(20_000_000, 19, 1052632, 11)]
    [InlineData(1, 30, 1, 1)]
    [InlineData(long.MaxValue, 1, long.MaxValue, 1)]
    public void Integer_allocation_exactly_conserves_target(long target, int count, long firstAmount, int sameAmountCount)
    {
        var result = FixedFundAllocation.Allocate(target, Enumerable.Range(1, count).ToArray());
        Assert.Equal(target, result.Values.Sum());
        Assert.Equal(firstAmount, result[1]);
        Assert.Equal(sameAmountCount, result.Values.Count(a => a == firstAmount));
    }

    [Fact]
    public void Vacancies_and_replacement_preserve_seat_order_without_shifting()
    {
        var first = FixedFundAllocation.Allocate(101, [9, 1, 3]);
        var second = FixedFundAllocation.Allocate(101, [3, 9, 1]);
        Assert.Equal(34, first[1]); Assert.Equal(34, first[3]); Assert.Equal(33, first[9]);
        Assert.All(first, item => Assert.Equal(item.Value, second[item.Key]));
    }

    [Fact]
    public void Invalid_membership_or_target_fails_closed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedFundAllocation.Allocate(0, [1]));
        Assert.Throws<ArgumentException>(() => FixedFundAllocation.Allocate(100, []));
        Assert.Throws<ArgumentException>(() => FixedFundAllocation.Allocate(100, [1, 1]));
        Assert.Throws<ArgumentException>(() => FixedFundAllocation.Allocate(100, [0, 31]));
    }
}
