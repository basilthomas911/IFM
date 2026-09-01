namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Authoritative futures market and position-permission state in US Eastern time.</summary>
public enum FuturesMarketState
{
    Closed = 0,
    OffTrading = 1,
    LiveTrading = 2
}

/// <summary>
/// Separates the 18:00-17:00 value-date session from the weekday 03:00-16:00
/// live-trading window without relying on a client-local clock.
/// </summary>
public static class FuturesMarketSessionPolicy
{
    public static readonly TimeOnly LiveTradingOpensAt = new(3, 0);
    public static readonly TimeOnly LiveTradingClosesAt = new(16, 0);

    public static FuturesMarketState GetState(DateTimeOffset instant)
    {
        if (!FuturesTradingValueDate.TryGet(instant, out _))
            return FuturesMarketState.Closed;

        var eastern = TimeZoneInfo.ConvertTime(instant, FuturesTradingValueDate.MarketTimeZone);
        var time = TimeOnly.FromDateTime(eastern.DateTime);
        return eastern.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday
            && time >= LiveTradingOpensAt
            && time < LiveTradingClosesAt
                ? FuturesMarketState.LiveTrading
                : FuturesMarketState.OffTrading;
    }

    /// <summary>Returns the next 03:00, 16:00, 17:00 or 18:00 state/value-date transition.</summary>
    public static DateTimeOffset GetNextTransitionUtc(DateTimeOffset instant)
    {
        var eastern = TimeZoneInfo.ConvertTime(instant, FuturesTradingValueDate.MarketTimeZone);
        var firstDate = DateOnly.FromDateTime(eastern.DateTime);
        var currentState = GetState(instant);
        FuturesTradingValueDate.TryGet(instant, out var currentValueDate);

        for (var dayOffset = 0; dayOffset <= 8; dayOffset++)
        {
            var date = firstDate.AddDays(dayOffset);
            foreach (var time in new[]
                     {
                         LiveTradingOpensAt,
                         LiveTradingClosesAt,
                         new TimeOnly(17, 0),
                         new TimeOnly(18, 0)
                     })
            {
                var local = date.ToDateTime(time, DateTimeKind.Unspecified);
                var utc = new DateTimeOffset(
                    TimeZoneInfo.ConvertTimeToUtc(local, FuturesTradingValueDate.MarketTimeZone),
                    TimeSpan.Zero);
                if (utc <= instant)
                    continue;

                var after = utc.AddTicks(1);
                var nextState = GetState(after);
                FuturesTradingValueDate.TryGet(after, out var nextValueDate);
                if (nextState != currentState || nextValueDate != currentValueDate)
                    return utc;
            }
        }

        throw new InvalidOperationException("The next futures market-state transition could not be resolved.");
    }
}
