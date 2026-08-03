using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public static class ScyllaDbObjectDataExtensions
{
    public static DateTime AsDateTime(this DateOnly dateOnly)
        => dateOnly.ToDateTime(TimeOnly.MinValue);

    public static DateOnly AsDateOnly(this DateTime dateTime)
        => new(dateTime.Year, dateTime.Month, dateTime.Day);

    public static LocalDate AsLocalDate(this DateTime dateTime)
        => new(dateTime.Year, dateTime.Month, dateTime.Day);

    public static LocalTime AsLocalTime(this DateTime dateTime)
        => new(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Nanosecond);

    public static int AsMilliseconds(this LocalTime localTime)
        => (int)(localTime.TotalNanoseconds % 1_000_000_000) % 1_000_000;

    public static int AsMicroseconds(this LocalTime localTime)
        => (int)(localTime.TotalNanoseconds % 1_000_000_000) % 1_000;
}
