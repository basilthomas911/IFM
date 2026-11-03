using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Event.Model;

public static class FuturesMacdSignalTimer
{

    extension(FuturesMacdSignalStartedEvent e)
    {
        public bool StartTimer(Func<FuturesMacdSignalEntityId, ValueTask> callback)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.Start(e.EntityId, callback, PeriodSignalTimerPeriod.Get(e.EntityId.TimePeriod));
        }

        internal bool StartTimer(Func<FuturesMacdSignalEntityId, ValueTask> callback, TimeSpan period)
            => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.Start(e.EntityId, callback, period);

        public bool TryAcceptSourceSequence(long sourceSequence)

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.TryAcceptSourceSequence(e.EntityId, sourceSequence);
        }
    }

    extension(FuturesMacdSignalStoppedEvent e)
    {
        public ValueTask<bool> StopTimerAsync()

        {
            ArgumentNullException.ThrowIfNull(e);
            return PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.StopAsync(e.EntityId);
        }
    }
    public static ValueTask StopAllAsync() => PeriodSignalTimerRegistry<FuturesMacdSignalEntityId>.StopAllAsync();
}
