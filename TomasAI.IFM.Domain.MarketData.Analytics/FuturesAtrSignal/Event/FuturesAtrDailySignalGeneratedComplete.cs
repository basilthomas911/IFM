using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event;

/// <summary>Handles completion of a daily ATR projection.</summary>
public static class FuturesAtrDailySignalGeneratedComplete
{

    extension(FuturesAtrDailySignalGeneratedCompleteEvent @event)
    {
        /// <summary>Accepts the completed daily ATR notification.</summary>
        public ValueTask<bool> ExecuteAsync(IFuturesAtrSignalEventContext context, ILogger<FuturesAtrSignalEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            return ValueTask.FromResult(true);
        }
    }
}
