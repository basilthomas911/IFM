using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event;

/// <summary>Handles one failed VWAP projection.</summary>
public static class FuturesVwapSignalUpdatedFail
{

    extension(FuturesVwapSignalUpdatedFailEvent @event)
    {
        /// <summary>Logs the typed projection failure and acknowledges its terminal event.</summary>
        public ValueTask<bool> ExecuteAsync(
            IFuturesVwapSignalEventContext context,
            ILogger<FuturesVwapSignalEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write35(logger, null, @event.EntityId, @event.ErrorMessage);
            return ValueTask.FromResult(true);
        }
    }
}
