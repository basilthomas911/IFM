namespace TomasAI.IFM.Shared.MarketDataAnalytics;

/// <summary>
/// Specifies the available time period intervals for trading operations.
/// </summary>
/// <remarks>Use this enumeration to define the granularity of trading data or to configure trading strategies
/// that depend on specific time intervals. The values represent common periods used in market analysis, such as daily,
/// hourly, or minute-based intervals.</remarks>
public enum TradeTimePeriodType
{
    None,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    TenSeconds,
    FifteenSeconds,
    OneMinute,
    FiveMinutes,
    TenMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    OneHour,
    WeekMonthBridge
}

public static class TradeTimePeriodTypeExtensions
{
    public static string ToStringFast(this TradeTimePeriodType value) => value switch
    {
        TradeTimePeriodType.None => nameof(TradeTimePeriodType.None),
        TradeTimePeriodType.Daily => nameof(TradeTimePeriodType.Daily),
        TradeTimePeriodType.Weekly => nameof(TradeTimePeriodType.Weekly),
        TradeTimePeriodType.Monthly => nameof(TradeTimePeriodType.Monthly),
        TradeTimePeriodType.Quarterly => nameof(TradeTimePeriodType.Quarterly),
        TradeTimePeriodType.TenSeconds => nameof(TradeTimePeriodType.TenSeconds),
        TradeTimePeriodType.FifteenSeconds => nameof(TradeTimePeriodType.FifteenSeconds),
        TradeTimePeriodType.OneMinute => nameof(TradeTimePeriodType.OneMinute),
        TradeTimePeriodType.FiveMinutes => nameof(TradeTimePeriodType.FiveMinutes),
        TradeTimePeriodType.TenMinutes => nameof(TradeTimePeriodType.TenMinutes),
        TradeTimePeriodType.FifteenMinutes => nameof(TradeTimePeriodType.FifteenMinutes),
        TradeTimePeriodType.ThirtyMinutes => nameof(TradeTimePeriodType.ThirtyMinutes),
        TradeTimePeriodType.OneHour => nameof(TradeTimePeriodType.OneHour),
        _ => value.ToString()
    };
}
