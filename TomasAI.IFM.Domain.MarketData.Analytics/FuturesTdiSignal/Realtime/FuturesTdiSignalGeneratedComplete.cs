using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Handles one completed TDI realtime projection.</summary>
public static class FuturesTdiSignalGeneratedComplete
{
    /// <summary>Forwards the completed signal to Market Outlook.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTdiSignalGeneratedCompleteEvent @event,
        IEventActorContext<FuturesTdiSignalRealtimeActor> eventContext,
        IFuturesTdiSignalRealtimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await eventContext.PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
        return true;
    }
}
