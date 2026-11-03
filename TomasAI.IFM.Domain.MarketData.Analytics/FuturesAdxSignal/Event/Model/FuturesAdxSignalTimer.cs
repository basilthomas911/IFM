using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;

public static class FuturesAdxSignalTimer
{

    extension(FuturesAdxSignalStartedEvent e)
    {
        public bool StartTimer(Func<FuturesAdxSignalEntityId, ValueTask> callback)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
        }

        internal bool StartTimer(Func<FuturesAdxSignalEntityId, ValueTask> callback, TimeSpan period)
            => PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.Start(e.EntityId, callback, period);

        public bool TryAcceptSourceSequence(long sourceSequence)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.TryAcceptSourceSequence(e.EntityId, sourceSequence);
        }
    }

    extension(FuturesAdxSignalStoppedEvent e)
    {
        public ValueTask<bool> StopTimerAsync()

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.StopAsync(e.EntityId);
        }
    }
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesAdxSignalEntityId>.StopAllAsync();
}
