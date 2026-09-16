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

/// <summary>Handles one MarketOutlookComponentChangedRealtimeEvent message.</summary>
public static class MarketOutlookComponentChanged
{
    /// <summary>Submits this realtime observation through the existing Market Outlook writer.</summary>
    public static ValueTask ExecuteAsync(
        this MarketOutlookComponentChangedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ILogger<MarketOutlookSnapshotRealtimeActor> logger)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        SubmitComponent(source, context);
        return ValueTask.CompletedTask;
    }

    static void SubmitComponent(
        MarketOutlookComponentChangedRealtimeEvent source,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        var eligible = MarketOutlookComponentEligibility.SelectEligible(source, out var ignoredReason);
        var typed = (IMarketOutlookSnapshotRealtimeContext)context;
        var writer = typed.UpdateWriter;
        var commandId = source.CommandId == Guid.Empty ? source.Id : source.CommandId;
        var submitted = 0;

        if (eligible.FuturesRsiSignal is { } rsi)
        {
            writer.Submit(new RsiMarketOutlookUpdate
            {
                UpdateId = source.Id,
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = rsi.Metadata?.MarketDataAsOfUtc.UtcDateTime ?? source.ReceivedOn,
                Signal = rsi,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
            submitted++;
        }
        if (eligible.FuturesTdiSignal is { } tdi)
        {
            writer.Submit(new TdiMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Tdi),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = source.ReceivedOn,
                Signal = tdi,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
            submitted++;
        }
        if (eligible.FuturesItiSignal is { } iti)
        {
            writer.Submit(new ItiMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Iti),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = source.ReceivedOn,
                Signal = iti,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
            submitted++;
        }
        if (eligible.VixFuturesPrice > 0)
        {
            writer.Submit(new VixPriceMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.VixPrice),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = source.ReceivedOn,
                Price = eligible.VixFuturesPrice,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
            submitted++;
        }
        if (eligible.FuturesEmaSignal is { } ema)
        {
            writer.Submit(new EmaMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Ema),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = ema.Metadata.MarketDataAsOfUtc.UtcDateTime,
                Signal = ema,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = ema.Metadata.SourceSequence
            });
            submitted++;
        }
        if (eligible.FuturesBbSignal is { } bb)
        {
            writer.Submit(new BollingerBandMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.BollingerBand),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = bb.Metadata.MarketDataAsOfUtc.UtcDateTime,
                Signal = bb,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = bb.Metadata.SourceSequence
            });
            submitted++;
        }
        if (eligible.FuturesTradeSignal is { } tradeSignal)
        {
            writer.Submit(new TradeSignalMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.TradeSignal),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = source.ReceivedOn,
                Signal = tradeSignal,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = source.EventId
            });
            submitted++;
        }

        if (eligible.FuturesVwapSignal is { } vwap)
        {
            writer.Submit(new VwapMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Vwap),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = vwap.AsOfUtc.UtcDateTime,
                Signal = vwap,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = vwap.LastTradeSourceSequence,
                StreamEpochId = vwap.StreamEpochId,
                StreamOrdinal = vwap.LastTradeOrdinal
            });
            submitted++;
        }
        if (eligible.FuturesAdxSignal is { } adx)
        {
            writer.Submit(new AdxMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Adx),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = adx.Metadata?.MarketDataAsOfUtc.UtcDateTime ?? source.ReceivedOn,
                Signal = adx,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = adx.Metadata?.SourceSequence ?? source.EventId
            });
            submitted++;
        }
        if (eligible.FuturesAtrSignal is { } atr)
        {
            writer.Submit(new AtrMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Atr),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = atr.Metadata?.MarketDataAsOfUtc.UtcDateTime ?? source.ReceivedOn,
                Signal = atr,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = atr.Metadata?.SourceSequence ?? source.EventId
            });
            submitted++;
        }
        if (eligible.FuturesMacdSignal is { } macd)
        {
            writer.Submit(new MacdMarketOutlookUpdate
            {
                UpdateId = ComponentId(source.Id, MarketOutlookUpdateKind.Macd),
                EntityId = source.EntityId,
                ReceivedAtUtc = source.ReceivedOn,
                MarketDataAsOfUtc = macd.Metadata?.MarketDataAsOfUtc.UtcDateTime ?? source.ReceivedOn,
                Signal = macd,
                CommandId = commandId,
                AggregateId = source.AggregateId,
                EventSource = source.EventSource,
                SourceSequence = macd.Metadata?.SourceSequence ?? source.EventId
            });
            submitted++;
        }
        if (submitted == 0 && !string.IsNullOrWhiteSpace(ignoredReason))
        {
            typed.Logger.LogDebug(
                "Ignored Market Outlook component {EventSource} for {EntityId}: {Reason}",
                source.EventSource,
                source.EntityId.Format(),
                ignoredReason);
        }
    }

    static Guid ComponentId(Guid sourceId, MarketOutlookUpdateKind kind)
    {
        Span<byte> bytes = stackalloc byte[16];
        sourceId.TryWriteBytes(bytes);
        bytes[15] ^= (byte)((int)kind + 1);
        return new Guid(bytes);
    }

}
