namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Specifies the available time period intervals for trading operations.
/// </summary>
/// <remarks>Use this enumeration to define the granularity of trading data or to configure trading strategies
/// that depend on specific time intervals. The values represent common periods used in market analysis, such as daily,
/// hourly, or minute-based intervals.</remarks>
public enum TimeFrameType
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
    public static string ToStringFast(this TimeFrameType value) => value switch
    {
        TimeFrameType.None => nameof(TimeFrameType.None),
        TimeFrameType.Daily => nameof(TimeFrameType.Daily),
        TimeFrameType.Weekly => nameof(TimeFrameType.Weekly),
        TimeFrameType.Monthly => nameof(TimeFrameType.Monthly),
        TimeFrameType.Quarterly => nameof(TimeFrameType.Quarterly),
        TimeFrameType.TenSeconds => nameof(TimeFrameType.TenSeconds),
        TimeFrameType.FifteenSeconds => nameof(TimeFrameType.FifteenSeconds),
        TimeFrameType.OneMinute => nameof(TimeFrameType.OneMinute),
        TimeFrameType.FiveMinutes => nameof(TimeFrameType.FiveMinutes),
        TimeFrameType.TenMinutes => nameof(TimeFrameType.TenMinutes),
        TimeFrameType.FifteenMinutes => nameof(TimeFrameType.FifteenMinutes),
        TimeFrameType.ThirtyMinutes => nameof(TimeFrameType.ThirtyMinutes),
        TimeFrameType.OneHour => nameof(TimeFrameType.OneHour),
        _ => value.ToString()
    };
}
