using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event;

/// <summary>Continues a successfully projected EMA event into Bollinger command processing.</summary>
public static class FuturesEmaSignalGeneratedComplete
{
    /// <summary>Sends the exact source bar and EMA result to the Bollinger command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesEmaSignalGeneratedCompleteEvent @event,
        IFuturesEmaSignalEventContext context, ILogger<FuturesEmaSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        await context.PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
        var result = await (context.GenerateFuturesBbSignalAsync(@event.Observation, @event.FuturesEmaSignal)).ConfigureAwait(false);
        if (result is ServiceFailed<GuidResult>)
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write40(logger, null, @event.FuturesEmaSignal.Metadata.ObservationId);
        return result is not ServiceFailed<GuidResult>;
    }
}
