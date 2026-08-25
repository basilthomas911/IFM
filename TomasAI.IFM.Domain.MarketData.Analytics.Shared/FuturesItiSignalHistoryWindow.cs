namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Defines the inclusive trading-value-date range represented by a Futures ITI timeframe.</summary>
public readonly record struct FuturesItiSignalHistoryWindow(
    DateOnly StartValueDate,
    DateOnly EndValueDate)
{
    /// <summary>Resolves the trailing Daily, Weekly, or Monthly range ending on <paramref name="valueDate"/>.</summary>
    public static FuturesItiSignalHistoryWindow Resolve(
        DateOnly valueDate,
        TimeFrameType timePeriod)
    {
        if (valueDate == default)
            throw new ArgumentOutOfRangeException(nameof(valueDate));

        return timePeriod switch
        {
            TimeFrameType.Daily => new(valueDate, valueDate),
            TimeFrameType.Weekly => new(valueDate.AddDays(-6), valueDate),
            TimeFrameType.Monthly => new(valueDate.AddMonths(-1), valueDate),
            _ => throw new ArgumentOutOfRangeException(
                nameof(timePeriod),
                timePeriod,
                "Futures ITI history supports only Daily, Weekly, and Monthly timeframes.")
        };
    }

}
