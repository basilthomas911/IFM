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

/// <summary>Handles one FuturesSessionStatisticsUpdatedRealtimeEvent message.</summary>
public static class FuturesSessionStatisticsUpdated
{
    /// <summary>Submits this realtime observation through the existing Market Outlook writer.</summary>
    public static ValueTask ExecuteAsync(
        this FuturesSessionStatisticsUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ILogger<MarketOutlookSnapshotRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return SubmitVxSessionStatisticsAsync(source, context);
    }

    static async ValueTask SubmitVxSessionStatisticsAsync(
        FuturesSessionStatisticsUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        var statistics = source.Statistics;
        if (!statistics.HasPriceStatistics)
            return;

        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        if (!MarketOutlookVxTargetResolver.TryResolveVxTarget(typed, statistics.ContractId, statistics.ValueDate, out var target))
            return;

        var price = await typed.MarketDataApi.GetFuturesPriceAsync(statistics.ContractId)
            .ConfigureAwait(false);
        typed.UpdateWriter.Submit(new VixPriceMarketOutlookUpdate
        {
            UpdateId = source.Id,
            EntityId = target,
            ReceivedAtUtc = source.ReceivedOn,
            MarketDataAsOfUtc = source.ReceivedOn,
            Price = price,
            SessionOpenPrice = statistics.OpenPrice,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = "MarketOutlookVxSessionOpenRefresh",
            SourceSequence = statistics.SourceSequence
        });
    }

}
