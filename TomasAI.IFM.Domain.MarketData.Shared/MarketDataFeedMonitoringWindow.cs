namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Defines the market-open interval in which an enabled live market-data feed
/// must be freshness-monitored. Position-entry permission is a separate policy.
/// </summary>
public static class MarketDataFeedMonitoringWindow
{
    public static readonly TimeOnly OpensAt = new(18, 0);
    public static readonly TimeOnly ClosesAt = new(17, 0);

    /// <summary>Gets whether live feed freshness is required at the supplied instant.</summary>
    public static bool IsOpen(DateTimeOffset utcNow)
        => FuturesTradingValueDate.TryGet(utcNow, out _);

    /// <summary>
    /// Gets the UTC start of the currently open monitoring window, or
    /// <see langword="null"/> when live feed freshness is not required.
    /// </summary>
    public static DateTimeOffset? GetCurrentStartUtc(DateTimeOffset utcNow)
    {
        if (!FuturesTradingValueDate.TryGet(utcNow, out var valueDate))
            return null;
        return FuturesTradingValueDate.GetSessionStartUtc(valueDate);
    }

    /// <summary>Gets the next weekday monitoring-window start after the supplied instant.</summary>
    public static DateTimeOffset GetNextStartUtc(DateTimeOffset utcNow)
    {
        if (!FuturesTradingValueDate.TryGet(utcNow, out var valueDate))
            return FuturesTradingValueDate.GetNextTransitionUtc(utcNow);
        return FuturesTradingValueDate.GetNextTransitionUtc(
            FuturesTradingValueDate.GetSessionEndUtc(valueDate));
    }
}
