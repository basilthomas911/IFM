using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Realtime/NATS adapter for Market Outlook. It validates routed source events and submits strongly
/// typed local updates; it never mutates or publishes the cache directly.
/// </summary>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = "MarketOutlook";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [MarketOutlookComponentChangedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!,
            [MarketOutlookSnapshotInsertedEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotInsertedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IEventActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        {
            [typeof(MarketOutlookComponentChangedRealtimeEvent)] = static (@event, context) =>
            {
                SubmitComponent((MarketOutlookComponentChangedRealtimeEvent)@event, context);
                return ValueTask.CompletedTask;
            },
            [typeof(MarketOutlookEodUpdatedRealtimeEvent)] = static (@event, context) =>
            {
                SubmitEod((MarketOutlookEodUpdatedRealtimeEvent)@event, context);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context) =>
            {
                SubmitEsTrade((FuturesMarketPriceUpdatedRealtimeEvent)@event, context);
                return ValueTask.CompletedTask;
            },
            [typeof(MarketOutlookSnapshotInsertedEvent)] = static (@event, context) =>
                ((MarketOutlookSnapshotInsertedEvent)@event).ExecuteAsync(context)
        };

    protected override ValueTask OnStartup(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override IEvent ParseMessage(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(@event, context);
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

        if (submitted == 0 && !string.IsNullOrWhiteSpace(ignoredReason))
        {
            typed.Logger.LogDebug(
                "Ignored Market Outlook component {EventSource} for {EntityId}: {Reason}",
                source.EventSource,
                source.EntityId.Format(),
                ignoredReason);
        }
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

    static Guid ComponentId(Guid sourceId, MarketOutlookUpdateKind kind)
    {
        Span<byte> bytes = stackalloc byte[16];
        sourceId.TryWriteBytes(bytes);
        bytes[15] ^= (byte)((int)kind + 1);
        return new Guid(bytes);
    }

    protected override ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(
            ActorName,
            exception,
            "Market Outlook local update submission failed for {EntityId}",
            @event.Subject.EntityId);
        return ValueTask.CompletedTask;
    }
}
