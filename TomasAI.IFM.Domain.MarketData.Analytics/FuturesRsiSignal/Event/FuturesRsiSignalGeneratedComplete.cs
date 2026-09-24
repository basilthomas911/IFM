using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Handles one completed intraday RSI event.</summary>
public static class FuturesRsiSignalGeneratedComplete
{
    /// <summary>Forwards warm, valid RSI to Market Outlook.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalGeneratedCompleteEvent completed,
        IFuturesRsiSignalEventContext context,
        ILogger<FuturesRsiSignalEventActor> logger)
    {
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
