namespace webProject.Helpers;

public static class TimeZoneHelper
{
    private static readonly TimeZoneInfo KyivTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Kiev");
    
    public static DateTime UtcNow => DateTime.UtcNow;
    
    public static DateTime ToKyivTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, KyivTimeZone);
    }
    
    public static DateTime FromKyivToUtc(DateTime kyivDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(kyivDateTime, KyivTimeZone);
    }
    
    public static DateTime KyivNow => ToKyivTime(DateTime.UtcNow);
}

