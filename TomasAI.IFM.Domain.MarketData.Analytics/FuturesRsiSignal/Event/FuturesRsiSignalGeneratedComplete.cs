using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Handles one completed intraday RSI event.</summary>
public static class FuturesRsiSignalGeneratedComplete
{

    extension(FuturesRsiSignalGeneratedCompleteEvent completed)
    {
        /// <summary>Forwards warm, valid RSI to Market Outlook.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesRsiSignalEventContext context,
            ILogger<FuturesRsiSignalEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(completed);
            ArgumentNullException.ThrowIfNull(logger);
            if (completed.FuturesRsiSignal is { IsWarm: true, RSI: >= 0d }
                && completed.FuturesRsiSignal.Metadata is { IsValid: true })
            {
                await ((IEventActorContext<FuturesRsiSignalEventActor>)context)
                    .PublishMarketOutlookComponentAsync(completed).ConfigureAwait(false);
            }
            return true;
        }
    }
}
