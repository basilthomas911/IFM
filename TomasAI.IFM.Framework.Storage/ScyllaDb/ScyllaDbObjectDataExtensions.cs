using Cassandra;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public static class ScyllaDbObjectDataExtensions
{

    extension(DateOnly dateOnly)
    {
        public DateTime AsDateTime()
            => dateOnly.ToDateTime(TimeOnly.MinValue);
    }

    extension(DateTime dateTime)
    {
        public DateOnly AsDateOnly()
            => new(dateTime.Year, dateTime.Month, dateTime.Day);

        public LocalDate AsLocalDate()
            => new(dateTime.Year, dateTime.Month, dateTime.Day);

        public LocalTime AsLocalTime()
            => new(dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Nanosecond);
    }

    extension(LocalTime localTime)
    {
        public int AsMilliseconds()

        {
            ArgumentNullException.ThrowIfNull(localTime);
            return (int)(localTime.TotalNanoseconds % 1_000_000_000) % 1_000_000;
        }

        public int AsMicroseconds()

        {
            ArgumentNullException.ThrowIfNull(localTime);
            return (int)(localTime.TotalNanoseconds % 1_000_000_000) % 1_000;
        }
    }
}
