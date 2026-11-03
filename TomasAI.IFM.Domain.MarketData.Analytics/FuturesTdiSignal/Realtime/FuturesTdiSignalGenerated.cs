using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Handles one source TDI generated message after realtime projection admission.</summary>
public static class FuturesTdiSignalGenerated
{

    extension(FuturesTdiSignalGeneratedEvent @event)
    {
        /// <summary>Keeps the source lifecycle message observational, as before.</summary>
        public ValueTask<bool> ExecuteAsync(
            IFuturesTdiSignalRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            return ValueTask.FromResult(true);
        }
    }
}
