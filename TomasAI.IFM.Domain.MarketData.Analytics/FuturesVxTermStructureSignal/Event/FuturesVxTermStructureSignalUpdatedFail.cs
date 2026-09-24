using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event;

/// <summary>Handles one failed VX term-structure projection.</summary>
public static class FuturesVxTermStructureSignalUpdatedFail
{
    /// <summary>Logs the typed projection failure and acknowledges its terminal event.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVxTermStructureSignalUpdatedFailEvent @event,
        IFuturesVxTermStructureSignalEventContext context,
        ILogger<FuturesVxTermStructureSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError("VX term-structure projection failed for {EntityId}: {ErrorMessage}",
            @event.EntityId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
