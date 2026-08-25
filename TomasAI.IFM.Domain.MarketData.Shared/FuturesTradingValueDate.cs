namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Authoritative futures-session value-date policy. The trading session changes
/// value date at 18:00 US Eastern time Sunday through Thursday.
/// </summary>
public static class FuturesTradingValueDate
{
    static readonly Lazy<TimeZoneInfo> EasternTimeZone = new(ResolveEasternTimeZone);

    public static TimeZoneInfo MarketTimeZone => EasternTimeZone.Value;

    /// <summary>Resolves a market-local timestamp, returning false while the weekend session is closed.</summary>
    public static bool TryGet(DateTime marketLocalTime, out DateOnly valueDate)
    {
        var calendarDate = DateOnly.FromDateTime(marketLocalTime);
        if (marketLocalTime.DayOfWeek == DayOfWeek.Saturday
            || (marketLocalTime.DayOfWeek == DayOfWeek.Sunday
                && marketLocalTime.TimeOfDay < TimeSpan.FromHours(18)))
        {
            valueDate = default;
            return false;
        }

        valueDate = marketLocalTime.DayOfWeek is >= DayOfWeek.Sunday and <= DayOfWeek.Thursday
            && marketLocalTime.TimeOfDay >= TimeSpan.FromHours(18)
                ? calendarDate.AddDays(1)
                : calendarDate;
        return true;
    }

    /// <summary>Resolves a UTC instant using the authoritative US Eastern market timezone.</summary>
    public static bool TryGet(DateTimeOffset instant, out DateOnly valueDate)
        => TryGet(TimeZoneInfo.ConvertTime(instant, MarketTimeZone).DateTime, out valueDate);

    /// <summary>
    /// Returns the active value date, or the most recent Friday during the closed
    /// weekend. This is intended for process startup; live ticks use <see cref="TryGet(DateTimeOffset, out DateOnly)"/>.
    /// </summary>
    public static DateOnly GetOperational(DateTimeOffset instant)
    {
        var marketLocal = TimeZoneInfo.ConvertTime(instant, MarketTimeZone).DateTime;
        if (TryGet(marketLocal, out var valueDate))
            return valueDate;

        var calendarDate = DateOnly.FromDateTime(marketLocal);
        return marketLocal.DayOfWeek == DayOfWeek.Saturday
            ? calendarDate.AddDays(-1)
            : calendarDate.AddDays(-2);
    }

    /// <summary>
    /// Returns the UTC start of the regular futures trading session represented
    /// by a value date. The session begins at 18:00 US Eastern on the preceding
    /// calendar date, with daylight-saving conversion supplied by the market timezone.
    /// </summary>
    public static DateTimeOffset GetSessionStartUtc(DateOnly valueDate)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        var localStart = valueDate.AddDays(-1).ToDateTime(
            new TimeOnly(18, 0),
            DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, MarketTimeZone);
        return new DateTimeOffset(utcStart, TimeSpan.Zero);
    }

    /// <summary>
    /// Returns the UTC end of the regular futures trading session represented by a value date.
    /// The session ends at 17:00 US Eastern on the value date.
    /// </summary>
    public static DateTimeOffset GetSessionEndUtc(DateOnly valueDate)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        var localEnd = valueDate.ToDateTime(
            new TimeOnly(17, 0),
            DateTimeKind.Unspecified);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, MarketTimeZone);
        return new DateTimeOffset(utcEnd, TimeSpan.Zero);
    }

    static TimeZoneInfo ResolveEasternTimeZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new TimeZoneNotFoundException(
            "Neither America/New_York nor Eastern Standard Time is available.");
    }
}
