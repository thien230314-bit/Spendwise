namespace SpendWise.Application.Common;

// Quy đổi ranh giới lịch Việt Nam sang UTC nhưng không thay đổi cách lưu timestamp.
public static class VietnamBusinessTime
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveTimeZone();

    public static DateTime ToLocal(DateTime utcTimestamp)
    {
        var utc = utcTimestamp.Kind switch
        {
            DateTimeKind.Utc => utcTimestamp,
            DateTimeKind.Local => utcTimestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    public static DateTime ToUtc(DateTime localBoundary)
    {
        // Boundary là giờ lịch Việt Nam, vì vậy không được hiểu nó là giờ máy chủ.
        var unspecified = DateTime.SpecifyKind(localBoundary, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
    }

    public static (DateTime StartUtc, DateTime EndUtc) MonthUtcRange(int year, int month)
    {
        var localStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return (ToUtc(localStart), ToUtc(localStart.AddMonths(1)));
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        // IANA dùng trên Linux; Windows ID giữ ứng dụng chạy được trên máy phát triển Windows cũ.
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Thử ID tương thích tiếp theo.
            }
            catch (InvalidTimeZoneException)
            {
                // Thử ID tương thích tiếp theo.
            }
        }

        throw new InvalidOperationException("Vietnam time zone is unavailable on this host.");
    }
}
