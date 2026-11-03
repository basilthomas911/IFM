using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event;

/// <summary>Handles one failed VX term-structure projection.</summary>
public static class FuturesVxTermStructureSignalUpdatedFail
{

    extension(FuturesVxTermStructureSignalUpdatedFailEvent @event)
    {
        /// <summary>Logs the typed projection failure and acknowledges its terminal event.</summary>
        public ValueTask<bool> ExecuteAsync(
            IFuturesVxTermStructureSignalEventContext context,
            ILogger<FuturesVxTermStructureSignalEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write32(logger, null, @event.EntityId, @event.ErrorMessage);
            return ValueTask.FromResult(true);
        }
    }
}
