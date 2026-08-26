using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Handles completion of a daily RSI projection.</summary>
public static class FuturesRsiDailySignalGeneratedComplete
{
    /// <summary>Accepts the completed daily RSI notification.</summary>
    public static ValueTask<bool> ExecuteAsync(this FuturesRsiDailySignalGeneratedCompleteEvent @event,
        IFuturesRsiSignalEventContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.FromResult(true);
    }
}
