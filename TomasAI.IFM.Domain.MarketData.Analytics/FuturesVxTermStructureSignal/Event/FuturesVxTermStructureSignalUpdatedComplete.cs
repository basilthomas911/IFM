using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Event;

/// <summary>Handles one successfully projected VX term-structure event.</summary>
public static class FuturesVxTermStructureSignalUpdatedComplete
{
    /// <summary>Acknowledges the completed projection without retaining state.</summary>
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
}
