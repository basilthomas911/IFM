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
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;

/// <summary>Handles one FuturesMarketPriceUpdatedRealtimeEvent message.</summary>
public static class FuturesMarketPriceUpdated
{
    /// <summary>Submits this realtime observation through the existing Market Outlook writer.</summary>
    public static ValueTask ExecuteAsync(
        this FuturesMarketPriceUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ILogger<MarketOutlookSnapshotRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return SubmitMarketPriceAsync(source, context);
    }

    static void SubmitEsTrade(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase)
            || trade.NormalizedTradeAction != NormalizedTradeAction.New
            || trade.LastPrice <= 0m
            || trade.LastSize == 0)
            return;

        ((IMarketOutlookSnapshotRealtimeContext)context).UpdateWriter.Submit(
            new EsTradeMarketOutlookUpdate
            {
                UpdateId = source.Id,
                EntityId = new(source.Price.ContractId, source.Price.ValueDate),
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = trade.EventTimestamp.UtcDateTime,
                PriceUpdate = source,
                CommandId = source.CommandId,
                AggregateId = source.AggregateId,
                EventSource = "MarketOutlookEsTradeRefresh",
                SourceSequence = source.EventId,
                StreamEpochId = trade.StreamEpochId,
                StreamOrdinal = trade.TradeOrdinal
            });
    }

    static async ValueTask SubmitMarketPriceAsync(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (source.Price.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase))
        {
            SubmitEsTrade(source, context);
            return;
        }

        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        if (!MarketOutlookVxTargetResolver.TryResolveVxTarget(typed, source.Price.ContractId, source.Price.ValueDate, out var target))
            return;

        var price = await typed.MarketDataApi.GetFuturesPriceAsync(source.Price.ContractId)
            .ConfigureAwait(false);
        if (price is not > 0m)
            return;

        decimal? sessionOpen = null;
        if (typed.MarketDataApi.TryGetFuturesSessionStatistics(
                source.Price.ContractId, out var statistics)
            && statistics.ValueDate == source.Price.ValueDate
            && statistics.OpenPrice > 0m)
            sessionOpen = statistics.OpenPrice;

        var (marketDataAsOfUtc, sourceSequence, streamEpochId, streamOrdinal) =
            VxSourcePosition(source);
        typed.UpdateWriter.Submit(new VixPriceMarketOutlookUpdate
        {
            UpdateId = source.Id,
            EntityId = target,
            ReceivedAtUtc = source.ReceivedOn,
            MarketDataAsOfUtc = marketDataAsOfUtc,
            Price = price,
            SessionOpenPrice = sessionOpen,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = "MarketOutlookVxPriceRefresh",
            SourceSequence = sourceSequence,
            StreamEpochId = streamEpochId,
            StreamOrdinal = streamOrdinal
        });
    }

    static (DateTime MarketDataAsOfUtc, long SourceSequence, Guid StreamEpochId, long StreamOrdinal)
        VxSourcePosition(FuturesMarketPriceUpdatedRealtimeEvent source)
    {
        if (source.UpdateSource == FuturesMarketPriceUpdateSource.Trade
            && source.Price.Trade is { } trade)
            return (trade.EventTimestamp.UtcDateTime, trade.SourceSequence,
                trade.StreamEpochId, trade.TradeOrdinal);
        if (source.UpdateSource == FuturesMarketPriceUpdateSource.Quote
            && source.Price.Quote is { } quote)
            return (quote.EventTimestamp.UtcDateTime, quote.SourceSequence, Guid.Empty, 0);
        return (source.ReceivedOn, source.EventId, Guid.Empty, 0);
    }

}
