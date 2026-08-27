using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Extensions;

/// <summary>Handles VWAP projection lifecycle events without retaining state.</summary>
public static class FuturesVwapSignalEvents
{
    /// <summary>Accepts a successful VWAP projection.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVwapSignalUpdatedCompleteEvent @event,
        IFuturesVwapSignalEventContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.FromResult(true);
    }

    /// <summary>Records a failed VWAP projection.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesVwapSignalUpdatedFailEvent @event,
        IFuturesVwapSignalEventContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError("VWAP projection failed for {EntityId}: {ErrorMessage}",
            @event.EntityId, @event.ErrorMessage);
        return ValueTask.FromResult(true);
    }
}
