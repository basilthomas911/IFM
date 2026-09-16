using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Event;

/// <summary>Handles one successfully projected VWAP event.</summary>
public static class FuturesVwapSignalUpdatedComplete
{
    /// <summary>Acknowledges the completed projection without retaining state.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesVwapSignalUpdatedCompleteEvent @event,
        IFuturesVwapSignalEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        await context.PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
        return true;
    }
}
