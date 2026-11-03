using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Handles one completed TDI realtime projection.</summary>
public static class FuturesTdiSignalGeneratedComplete
{

    extension(FuturesTdiSignalGeneratedCompleteEvent @event)
    {
        /// <summary>Forwards the completed signal to Market Outlook.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IEventActorContext<FuturesTdiSignalRealtimeActor> eventContext,
            IFuturesTdiSignalRealtimeContext context)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            await eventContext.PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
            return true;
        }
    }
}
