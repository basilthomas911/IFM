using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;

public static class FuturesMacdSignalTimer
{
    public static bool StartTimer(this FuturesMacdSignalStartedEvent e, Func<FuturesMacdSignalEntityId, ValueTask> callback)
        => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
    internal static bool StartTimer(this FuturesMacdSignalStartedEvent e, Func<FuturesMacdSignalEntityId, ValueTask> callback, TimeSpan period)
        => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.Start(e.EntityId, callback, period);
    public static ValueTask<bool> StopTimerAsync(this FuturesMacdSignalStoppedEvent e)
        => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.StopAsync(e.EntityId);
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.StopAllAsync();
}
