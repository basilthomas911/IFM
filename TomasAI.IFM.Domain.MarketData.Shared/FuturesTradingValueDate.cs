namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Authoritative futures-session value-date policy. The trading session changes
/// value date at 18:00 US Eastern time Sunday through Thursday.
/// </summary>
public static class FuturesTradingValueDate
{
    static readonly TimeOnly MarketOpensAt = new(18, 0);
    static readonly TimeOnly MarketClosesAt = new(17, 0);
    static readonly Lazy<TimeZoneInfo> EasternTimeZone = new(ResolveEasternTimeZone);

    public static TimeZoneInfo MarketTimeZone => EasternTimeZone.Value;

    /// <summary>
    /// Resolves a market-local timestamp, returning false during the daily
    /// 17:00-18:00 maintenance close and the Friday-to-Sunday weekend close.
    /// </summary>
    public static bool TryGet(DateTime marketLocalTime, out DateOnly valueDate)
    {
        var calendarDate = DateOnly.FromDateTime(marketLocalTime);
        var marketTime = TimeOnly.FromDateTime(marketLocalTime);
        var isClosed = marketLocalTime.DayOfWeek switch
        {
            DayOfWeek.Friday => marketTime >= MarketClosesAt,
            DayOfWeek.Saturday => true,
            DayOfWeek.Sunday => marketTime < MarketOpensAt,
            _ => marketTime >= MarketClosesAt && marketTime < MarketOpensAt
        };
        if (isClosed)
        {
            valueDate = default;
            return false;
        }

        valueDate = marketLocalTime.DayOfWeek is >= DayOfWeek.Sunday and <= DayOfWeek.Thursday
            && marketTime >= MarketOpensAt
                ? calendarDate.AddDays(1)
                : calendarDate;
        return true;
    }

    /// <summary>Resolves a UTC instant using the authoritative US Eastern market timezone.</summary>
    public static bool TryGet(DateTimeOffset instant, out DateOnly valueDate)
        => TryGet(TimeZoneInfo.ConvertTime(instant, MarketTimeZone).DateTime, out valueDate);

    /// <summary>
    /// Returns the active value date, or the most recently completed value date
    /// during a maintenance/weekend close. This is intended for process startup
    /// and read-only operation; live ticks use <see cref="TryGet(DateTimeOffset, out DateOnly)"/>.
    /// </summary>
    public static DateOnly GetOperational(DateTimeOffset instant)
    {
        var marketLocal = TimeZoneInfo.ConvertTime(instant, MarketTimeZone).DateTime;
        if (TryGet(marketLocal, out var valueDate))
            return valueDate;

        var calendarDate = DateOnly.FromDateTime(marketLocal);
        return marketLocal.DayOfWeek switch
        {
            DayOfWeek.Saturday => calendarDate.AddDays(-1),
            DayOfWeek.Sunday => calendarDate.AddDays(-2),
            _ => calendarDate
        };
    }

    /// <summary>
    /// Returns the next instant at which the active futures value date opens,
    /// closes, or rolls. The result is always strictly after <paramref name="instant"/>.
    /// </summary>
    public static DateTimeOffset GetNextTransitionUtc(DateTimeOffset instant)
    {
        var marketLocal = TimeZoneInfo.ConvertTime(instant, MarketTimeZone);
        var date = DateOnly.FromDateTime(marketLocal.DateTime);
        var time = TimeOnly.FromDateTime(marketLocal.DateTime);
        var (transitionDate, transitionTime) = marketLocal.DayOfWeek switch
        {
            DayOfWeek.Friday when time < MarketClosesAt => (date, MarketClosesAt),
            DayOfWeek.Friday => (date.AddDays(2), MarketOpensAt),
            DayOfWeek.Saturday => (date.AddDays(1), MarketOpensAt),
            DayOfWeek.Sunday when time < MarketOpensAt => (date, MarketOpensAt),
            DayOfWeek.Sunday => (date.AddDays(1), MarketClosesAt),
            _ when time < MarketClosesAt => (date, MarketClosesAt),
            _ when time < MarketOpensAt => (date, MarketOpensAt),
            _ => (date.AddDays(1), MarketClosesAt)
        };

        var localTransition = transitionDate.ToDateTime(
            transitionTime,
            DateTimeKind.Unspecified);
        var utcTransition = TimeZoneInfo.ConvertTimeToUtc(localTransition, MarketTimeZone);
        return new DateTimeOffset(utcTransition, TimeSpan.Zero);
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
