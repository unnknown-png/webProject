namespace webProject.Helpers;

public static class TimeZoneHelper
{
    // Kyiv timezone (UTC+2 standard, UTC+3 daylight saving)
    private static readonly TimeZoneInfo KyivTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
    
    /// <summary>
    /// Gets current time in UTC (PostgreSQL requires UTC DateTime for timestamp with time zone)
    /// Use ToKyivTime() for display in UI
    /// </summary>
    public static DateTime UtcNow => DateTime.UtcNow;
    
    /// <summary>
    /// Converts UTC DateTime to Kyiv timezone for display
    /// </summary>
    public static DateTime ToKyivTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, KyivTimeZone);
    }
    
    /// <summary>
    /// Converts Kyiv DateTime to UTC for database storage
    /// </summary>
    public static DateTime FromKyivToUtc(DateTime kyivDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(kyivDateTime, KyivTimeZone);
    }
    
    /// <summary>
    /// Gets current time displayed in Kyiv timezone
    /// </summary>
    public static DateTime KyivNow => ToKyivTime(DateTime.UtcNow);
}

