using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

internal static class PeriodSignalTimerPeriod
{
    public static TimeSpan Get(TimeFrameType timePeriod) => timePeriod switch
    {
        TimeFrameType.TenSeconds => TimeSpan.FromSeconds(10),
        TimeFrameType.FifteenSeconds => TimeSpan.FromSeconds(15),
        TimeFrameType.OneMinute => TimeSpan.FromMinutes(1),
        TimeFrameType.FiveMinutes => TimeSpan.FromMinutes(5),
        TimeFrameType.TenMinutes => TimeSpan.FromMinutes(10),
        TimeFrameType.FifteenMinutes => TimeSpan.FromMinutes(15),
        TimeFrameType.ThirtyMinutes => TimeSpan.FromMinutes(30),
        TimeFrameType.OneHour => TimeSpan.FromHours(1),
        TimeFrameType.FourHours => TimeSpan.FromHours(4),
        TimeFrameType.Daily => TimeSpan.FromMinutes(1),
        TimeFrameType.Weekly => TimeSpan.FromMinutes(15),
        TimeFrameType.WeekMonthBridge => TimeSpan.FromHours(1),
        TimeFrameType.Monthly => TimeSpan.FromDays(1),
        _ => throw new ArgumentOutOfRangeException(nameof(timePeriod), timePeriod, "Unsupported signal sampling period")
    };
}
