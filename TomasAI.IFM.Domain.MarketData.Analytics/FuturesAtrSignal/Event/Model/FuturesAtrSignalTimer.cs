using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;

public static class FuturesAtrSignalTimer
{

    extension(FuturesAtrSignalStartedEvent e)
    {
        public bool StartTimer(Func<FuturesAtrSignalEntityId, ValueTask> callback)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
        }

        internal bool StartTimer(Func<FuturesAtrSignalEntityId, ValueTask> callback, TimeSpan period)
            => PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.Start(e.EntityId, callback, period);

        public bool TryAcceptSourceSequence(long sourceSequence)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.TryAcceptSourceSequence(e.EntityId, sourceSequence);
        }
    }

    extension(FuturesAtrSignalStoppedEvent e)
    {
        public ValueTask<bool> StopTimerAsync()

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.StopAsync(e.EntityId);
        }
    }
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesAtrSignalEntityId>.StopAllAsync();
}
