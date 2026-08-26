using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Event;

/// <summary>Continues a successfully projected EMA event into Bollinger command processing.</summary>
public static class FuturesEmaSignalGeneratedComplete
{
    /// <summary>Sends the exact source bar and EMA result to the Bollinger command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesEmaSignalGeneratedCompleteEvent @event,
        IFuturesEmaSignalEventContext context, ILogger logger)
    {
        var result = await context.GenerateFuturesBbSignalAsync(@event.Observation, @event.Signal);
        if (result is ServiceFailed<GuidResult>)
            logger.LogError("Bollinger command rejected EMA observation {ObservationId}.",
                @event.Signal.Metadata.ObservationId);
        return result is not ServiceFailed<GuidResult>;
    }
}
