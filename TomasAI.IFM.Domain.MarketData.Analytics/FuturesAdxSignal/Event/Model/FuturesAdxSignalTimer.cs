using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;

public static class FuturesAdxSignalTimer
{
    public static bool StartTimer(this FuturesAdxSignalStartedEvent e, Func<FuturesAdxSignalEntityId, ValueTask> callback)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
    }
    internal static bool StartTimer(this FuturesAdxSignalStartedEvent e, Func<FuturesAdxSignalEntityId, ValueTask> callback, TimeSpan period)
        => PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.Start(e.EntityId, callback, period);
    public static bool TryAcceptSourceSequence(this FuturesAdxSignalStartedEvent e, long sourceSequence)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.TryAcceptSourceSequence(e.EntityId, sourceSequence);
    }
    public static ValueTask<bool> StopTimerAsync(this FuturesAdxSignalStoppedEvent e)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.StopAsync(e.EntityId);
    }
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.StopAllAsync();
}
