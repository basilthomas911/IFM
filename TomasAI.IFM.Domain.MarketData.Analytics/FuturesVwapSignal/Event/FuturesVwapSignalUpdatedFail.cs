using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event;

/// <summary>Handles one failed VWAP projection.</summary>
public static class FuturesVwapSignalUpdatedFail
{
    /// <summary>Logs the typed projection failure and acknowledges its terminal event.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVwapSignalUpdatedFailEvent @event,
        IFuturesVwapSignalEventContext context,
        ILogger<FuturesVwapSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError("VWAP projection failed for {EntityId}: {ErrorMessage}",
            @event.EntityId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
