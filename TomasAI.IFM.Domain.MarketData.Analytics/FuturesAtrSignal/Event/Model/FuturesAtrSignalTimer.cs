using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;

public static class FuturesAtrSignalTimer
{
    public static bool StartTimer(this FuturesAtrSignalStartedEvent e, Func<FuturesAtrSignalEntityId, ValueTask> callback)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
    }
    internal static bool StartTimer(this FuturesAtrSignalStartedEvent e, Func<FuturesAtrSignalEntityId, ValueTask> callback, TimeSpan period)
        => PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.Start(e.EntityId, callback, period);
    public static bool TryAcceptSourceSequence(this FuturesAtrSignalStartedEvent e, long sourceSequence)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.TryAcceptSourceSequence(e.EntityId, sourceSequence);
    }
    public static ValueTask<bool> StopTimerAsync(this FuturesAtrSignalStoppedEvent e)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.StopAsync(e.EntityId);
    }
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.StopAllAsync();
}
