namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Provider-neutral route-health classifications for one owned feed route.</summary>
public enum MarketDataFeedSessionHealthState
{
    Inactive = 0,
    OffHoursActive = 1,
    OffHoursDegraded = 2,
    Green = 3,
    Yellow = 4,
    Red = 5
}

/// <summary>Deterministic session-aware policy over accepted hot-cache updates.</summary>
public static class MarketDataFeedSessionHealthPolicy
{
    public static readonly TimeSpan GreenLimit = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan DegradedLimit = TimeSpan.FromMinutes(15);

    public static MarketDataFeedSessionHealthState Evaluate(
        FuturesMarketState marketState,
        DateTimeOffset utcNow,
        DateTimeOffset routeActivationUtc,
        DateTimeOffset? lastAcceptedCacheUpdateUtc,
        bool routeActive,
        bool routeConfiguredAndRunning)
    {
        if (marketState == FuturesMarketState.Closed || !routeActive)
            return MarketDataFeedSessionHealthState.Inactive;
        if (!routeConfiguredAndRunning)
            return marketState == FuturesMarketState.LiveTrading
                ? MarketDataFeedSessionHealthState.Red
                : MarketDataFeedSessionHealthState.OffHoursDegraded;

        var reference = lastAcceptedCacheUpdateUtc is { } accepted
            && accepted > routeActivationUtc
                ? accepted
                : routeActivationUtc;
        if (marketState == FuturesMarketState.LiveTrading)
        {
            var liveStart = MarketDataFeedMonitoringWindow.GetCurrentStartUtc(utcNow) ?? utcNow;
            if (reference < liveStart)
                reference = liveStart;
        }

        var age = utcNow <= reference ? TimeSpan.Zero : utcNow - reference;
        if (marketState == FuturesMarketState.OffTrading)
            return age <= DegradedLimit
                ? MarketDataFeedSessionHealthState.OffHoursActive
                : MarketDataFeedSessionHealthState.OffHoursDegraded;
        return age <= GreenLimit
            ? MarketDataFeedSessionHealthState.Green
            : age <= DegradedLimit
                ? MarketDataFeedSessionHealthState.Yellow
                : MarketDataFeedSessionHealthState.Red;
    }
}
