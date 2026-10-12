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
    WeekMonthBridge,
    FourHours
}
