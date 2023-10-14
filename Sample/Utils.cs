namespace Sample;


public static class Utils
{
    public static DateTimeOffset ToEndOfDay(this DateTimeOffset value)
        => new DateTimeOffset(value.Year, value.Month, value.Day, 23, 59, 59, value.Offset);
}