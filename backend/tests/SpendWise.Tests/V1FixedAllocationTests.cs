using SpendWise.Domain.V1Groups;
using Xunit;

namespace SpendWise.Tests;

public sealed class V1FixedAllocationTests
{
    [Theory]
    [InlineData(9_000_000, 3, 3_000_000)]
    [InlineData(10_000_000, 2, 5_000_000)]
    [InlineData(30, 30, 1)]
    [InlineData(long.MaxValue, 1, long.MaxValue)]
    public void Exactly_divisible_allocation_assigns_identical_amounts(long target, int count, long expectedEach)
    {
        var result = FixedFundAllocation.Allocate(target, Enumerable.Range(1, count).ToArray());
        Assert.Equal(target, result.Values.Sum());
        Assert.All(result.Values, v => Assert.Equal(expectedEach, v));
    }

    [Theory]
    [InlineData(10_000_000, 3)]
    [InlineData(101, 3)]
    [InlineData(1, 30)]
    public void Indivisible_target_throws_argument_exception(long target, int count)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            FixedFundAllocation.Allocate(target, Enumerable.Range(1, count).ToArray()));
        Assert.Contains("exactly divisible", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vacancies_and_replacement_preserve_seat_order_when_divisible()
    {
        var first = FixedFundAllocation.Allocate(90, [9, 1, 3]);
        var second = FixedFundAllocation.Allocate(90, [3, 9, 1]);
        Assert.Equal(30, first[1]); Assert.Equal(30, first[3]); Assert.Equal(30, first[9]);
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
