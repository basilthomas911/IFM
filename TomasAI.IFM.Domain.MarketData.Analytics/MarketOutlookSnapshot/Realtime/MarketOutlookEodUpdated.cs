using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;

/// <summary>Handles one MarketOutlookEodUpdatedRealtimeEvent message.</summary>
public static class MarketOutlookEodUpdated
{
    /// <summary>Submits this realtime observation through the existing Market Outlook writer.</summary>
    public static ValueTask ExecuteAsync(
        this MarketOutlookEodUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ILogger<MarketOutlookSnapshotRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        SubmitEod(source, context);
        return ValueTask.CompletedTask;
    }

    static void SubmitEod(
        MarketOutlookEodUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (!string.Equals(source.FuturesEodData.Symbol, "ES", StringComparison.OrdinalIgnoreCase))
            return;
        ((IMarketOutlookSnapshotRealtimeContext)context).UpdateWriter.Submit(
            new EodMarketOutlookUpdate
            {
                UpdateId = source.Id,
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = source.ReceivedOn,
                Eod = source.FuturesEodData,
                CommandId = source.CommandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
    }

}
