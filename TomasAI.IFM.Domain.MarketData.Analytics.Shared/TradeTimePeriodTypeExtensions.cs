namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

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
        TimeFrameType.FourHours => nameof(TimeFrameType.FourHours),
        _ => value.ToString()
    };
}
