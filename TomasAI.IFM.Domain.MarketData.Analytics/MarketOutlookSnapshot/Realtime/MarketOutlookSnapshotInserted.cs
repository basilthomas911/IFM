using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;

/// <summary>Handles one durably projected Market Outlook snapshot notification.</summary>
public static class MarketOutlookSnapshotInserted
{

    extension(MarketOutlookSnapshotInsertedEvent @event)
    {
        /// <summary>Preserves the intentional terminal realtime no-op.</summary>
        public ValueTask ExecuteAsync(
            IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
            ILogger<MarketOutlookSnapshotRealtimeActor> logger)
        {
            ArgumentNullException.ThrowIfNull(@event);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            return ValueTask.CompletedTask;
        }
    }
}
