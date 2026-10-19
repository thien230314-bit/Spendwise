namespace SpendWise.Domain.V1Groups;

public static class FixedFundAllocation
{
    public static IReadOnlyDictionary<int, long> Allocate(long targetVnd, IReadOnlyCollection<int> activeSeats)
    {
        if (targetVnd <= 0) throw new ArgumentOutOfRangeException(nameof(targetVnd));
        var ordered = activeSeats.Order().ToArray();
        if (ordered.Length is < 1 or > 30 || ordered.Distinct().Count() != ordered.Length || ordered.Any(s => s is < 1 or > 30))
            throw new ArgumentException("Use distinct occupied Fixed fund seats 1–30.", nameof(activeSeats));
        var quotient = targetVnd / ordered.Length;
        var remainder = targetVnd % ordered.Length;
        return ordered.Select((seat, index) => (seat, amount: quotient + (index < remainder ? 1 : 0)))
            .ToDictionary(item => item.seat, item => item.amount);
    }
}
