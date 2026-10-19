using SpendWise.Domain.Planning;

namespace SpendWise.Application.Features.RecurringTransactions;

// Tính lịch kế tiếp từ anchor gốc để ngày 31 và 29/02 không bị trôi.
public static class RecurringSchedule
{
    public static DateTime Next(DateTime current, RecurringFrequency frequency, int anchorDay, int anchorMonth)
    {
        current = current.ToUniversalTime();
        return frequency switch
        {
            RecurringFrequency.Daily => current.AddDays(1),
            RecurringFrequency.Weekly => current.AddDays(7),
            RecurringFrequency.Monthly => Monthly(current, anchorDay),
            RecurringFrequency.Yearly => Yearly(current, anchorDay, anchorMonth),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency))
        };
    }

    public static DateTime AdvanceUntilFuture(DateTime current, RecurringFrequency frequency,
        int anchorDay, int anchorMonth, DateTime utcNow)
    {
        var value = current;
        while (value <= utcNow)
        {
            value = Next(value, frequency, anchorDay, anchorMonth);
        }
        return value;
    }

    private static DateTime Monthly(DateTime current, int anchorDay)
    {
        var first = new DateTime(current.Year, current.Month, 1, current.Hour,
            current.Minute, current.Second, DateTimeKind.Utc).AddMonths(1);
        var intendedDay = anchorDay is >= 1 and <= 31 ? anchorDay : current.Day;
        var day = Math.Min(intendedDay, DateTime.DaysInMonth(first.Year, first.Month));
        return new DateTime(first.Year, first.Month, day, current.Hour,
            current.Minute, current.Second, current.Millisecond, DateTimeKind.Utc);
    }

    private static DateTime Yearly(DateTime current, int anchorDay, int anchorMonth)
    {
        var year = current.Year + 1;
        var month = anchorMonth is >= 1 and <= 12 ? anchorMonth : current.Month;
        var intendedDay = anchorDay is >= 1 and <= 31 ? anchorDay : current.Day;
        var day = Math.Min(intendedDay, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day, current.Hour, current.Minute,
            current.Second, current.Millisecond, DateTimeKind.Utc);
    }
}
