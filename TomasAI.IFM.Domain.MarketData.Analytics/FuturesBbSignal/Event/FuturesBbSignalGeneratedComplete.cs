using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Event;

/// <summary>Handles one completed Bollinger signal event.</summary>
public static class FuturesBbSignalGeneratedComplete
{
    /// <summary>Publishes the successfully projected signal to Market Outlook.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesBbSignalGeneratedCompleteEvent @event,
        IEventActorContext<FuturesBbSignalEventActor> context,
        ILogger<FuturesBbSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        await context.PublishMarketOutlookComponentAsync(@event).ConfigureAwait(false);
        return true;
    }
}
