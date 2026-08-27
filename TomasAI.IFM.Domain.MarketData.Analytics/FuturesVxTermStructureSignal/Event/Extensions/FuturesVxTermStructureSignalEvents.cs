using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Extensions;

/// <summary>Handles projected VX term-structure lifecycle events without retaining state.</summary>
public static class FuturesVxTermStructureSignalEvents
{
    /// <summary>Accepts a successfully projected VX curve.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVxTermStructureSignalUpdatedCompleteEvent @event,
        IFuturesVxTermStructureSignalEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.FromResult(true);
    }

    /// <summary>Records a failed VX curve projection.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVxTermStructureSignalUpdatedFailEvent @event,
        IFuturesVxTermStructureSignalEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError("VX term-structure projection failed for {EntityId}: {ErrorMessage}",
            @event.EntityId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
