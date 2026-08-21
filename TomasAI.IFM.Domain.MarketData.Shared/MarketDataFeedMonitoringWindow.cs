namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Defines when live market-data feeds are required for position-entry workflows.
/// Feed inactivity outside this weekday Eastern-time window is expected and must
/// not make the rest of the application unavailable.
/// </summary>
public static class MarketDataFeedMonitoringWindow
{
    public static readonly TimeOnly OpensAt = new(3, 0);
    public static readonly TimeOnly ClosesAt = new(16, 0);

    /// <summary>Gets whether live feed freshness is required at the supplied instant.</summary>
    public static bool IsOpen(DateTimeOffset utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTime(
            utcNow,
            FuturesTradingValueDate.MarketTimeZone);
        var easternTime = TimeOnly.FromDateTime(easternNow.DateTime);
        return IsTradingDay(easternNow.DayOfWeek)
            && easternTime >= OpensAt
            && easternTime < ClosesAt;
    }

    /// <summary>
    /// Gets the UTC start of the currently open monitoring window, or
    /// <see langword="null"/> when live feed freshness is not required.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTime(
            utcNow,
            FuturesTradingValueDate.MarketTimeZone);
        if (!IsOpen(utcNow))
            return null;

        var easternStart = new DateTime(
            easternNow.Year,
            easternNow.Month,
            easternNow.Day,
            OpensAt.Hour,
            OpensAt.Minute,
            0,
            DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(
            easternStart,
            FuturesTradingValueDate.MarketTimeZone);
        return new DateTimeOffset(utcStart, TimeSpan.Zero);
    }

    /// <summary>Gets the next weekday monitoring-window start after the supplied instant.</summary>
    public static DateTimeOffset GetNextStartUtc(DateTimeOffset utcNow)
    {
        var easternNow = TimeZoneInfo.ConvertTime(
            utcNow,
            FuturesTradingValueDate.MarketTimeZone);
        var nextDate = DateOnly.FromDateTime(easternNow.DateTime);
        if (IsTradingDay(easternNow.DayOfWeek)
            && TimeOnly.FromDateTime(easternNow.DateTime) < OpensAt)
        {
            return ToUtc(nextDate, OpensAt);
        }

        do
        {
            nextDate = nextDate.AddDays(1);
        }
        while (!IsTradingDay(nextDate.DayOfWeek));

        return ToUtc(nextDate, OpensAt);
    }

    static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var easternDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(
            easternDateTime,
            FuturesTradingValueDate.MarketTimeZone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    static bool IsTradingDay(DayOfWeek dayOfWeek)
        => dayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
}
