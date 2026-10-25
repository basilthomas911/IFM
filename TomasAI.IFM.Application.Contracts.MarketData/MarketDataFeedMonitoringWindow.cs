namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Resolves session-aware feed-health mode. Live-trading and off-trading use
/// different severity rules while the 17:00-18:00/weekend close is inactive.
/// </summary>
public static class MarketDataFeedMonitoringWindow
{
    public static readonly TimeOnly OpensAt = FuturesMarketSessionPolicy.LiveTradingOpensAt;
    public static readonly TimeOnly ClosesAt = FuturesMarketSessionPolicy.LiveTradingClosesAt;

    public static FuturesMarketState GetState(DateTimeOffset utcNow)
        => FuturesMarketSessionPolicy.GetState(utcNow);

    /// <summary>Gets whether live feed freshness is required at the supplied instant.</summary>
    public static bool IsOpen(DateTimeOffset utcNow)
        => GetState(utcNow) == FuturesMarketState.LiveTrading;

    /// <summary>
    /// Gets the UTC start of the currently open monitoring window, or
    /// <see langword="null"/> when live feed freshness is not required.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
    {
        if (!IsOpen(utcNow))
            return null;
        var eastern = TimeZoneInfo.ConvertTime(utcNow, FuturesTradingValueDate.MarketTimeZone);
        var localStart = DateOnly.FromDateTime(eastern.DateTime).ToDateTime(
            OpensAt,
            DateTimeKind.Unspecified);
        return new DateTimeOffset(
            TimeZoneInfo.ConvertTimeToUtc(localStart, FuturesTradingValueDate.MarketTimeZone),
            TimeSpan.Zero);
    }

    /// <summary>Gets the next weekday monitoring-window start after the supplied instant.</summary>
    public static DateTimeOffset GetNextStartUtc(DateTimeOffset utcNow)
    {
        var eastern = TimeZoneInfo.ConvertTime(utcNow, FuturesTradingValueDate.MarketTimeZone);
        var date = DateOnly.FromDateTime(eastern.DateTime);
        for (var offset = 0; offset <= 8; offset++)
        {
            var candidateDate = date.AddDays(offset);
            if (candidateDate.DayOfWeek is not (DayOfWeek.Monday or DayOfWeek.Tuesday
                or DayOfWeek.Wednesday or DayOfWeek.Thursday or DayOfWeek.Friday))
                continue;
            var localStart = candidateDate.ToDateTime(OpensAt, DateTimeKind.Unspecified);
            var candidate = new DateTimeOffset(
                TimeZoneInfo.ConvertTimeToUtc(localStart, FuturesTradingValueDate.MarketTimeZone),
                TimeSpan.Zero);
            if (candidate > utcNow)
                return candidate;
        }
        throw new InvalidOperationException("The next live-trading monitoring start could not be resolved.");
    }
}
