using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using CacheComponentType = TomasAI.IFM.Application.MarketData.MarketOutlook.MarketOutlookComponentType;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Owns the process-local, latest-arrival Market Outlook projection. Every eligible component and
/// every valid ES last trade atomically merges partial state, replaces the immutable whole snapshot,
/// and independently notifies UI clients.
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
    static readonly IMarketOutlookHotCache Cache = MarketOutlookHotCache.Shared;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> ParseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [MarketOutlookComponentChangedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!
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
        IActorMessage message) => ParseMappedRealtimeEvent(context, message, ParseMap);

    protected override async ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        var realtimeContext = (IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>)context;
        switch (@event)
        {
            case MarketOutlookComponentChangedRealtimeEvent component:
                await ApplyComponentAsync(component, realtimeContext).ConfigureAwait(false);
                break;
            case MarketOutlookEodUpdatedRealtimeEvent eod:
                await ApplyEodAsync(eod, realtimeContext).ConfigureAwait(false);
                break;
            case FuturesMarketPriceUpdatedRealtimeEvent price:
                await ApplyEsTradeAsync(price, realtimeContext).ConfigureAwait(false);
                break;
        }
    }

    static async ValueTask ApplyComponentAsync(
        MarketOutlookComponentChangedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        var eligible = MarketOutlookComponentEligibility.SelectEligible(source, out var ignoredReason);
        var position = Position(source.Id, source.EventId, source.ReceivedOn);
        List<MarketOutlookComponentWrite> components = [];
        if (eligible.FuturesRsiSignal is not null) components.Add(new(CacheComponentType.Rsi, position));
        if (eligible.FuturesTdiSignal is not null) components.Add(new(CacheComponentType.Tdi, position));
        if (eligible.FuturesItiSignal is { } iti)
        {
            components.Add(new(CacheComponentType.ItiLatest, position));
            var milestone = iti.IntrinsicTimeMode switch
            {
                IntrinsicTimeModeType.TrendDirectionChanged => CacheComponentType.ItiDirection,
                IntrinsicTimeModeType.TrendExtremeChanged => CacheComponentType.ItiExtreme,
                IntrinsicTimeModeType.TrendReversalChanged => CacheComponentType.ItiReversal,
                _ => (CacheComponentType?)null
            };
            if (milestone is { } component) components.Add(new(component, position));
        }
        if (eligible.VixFuturesPrice > 0) components.Add(new(CacheComponentType.Vx, position));
        if (eligible.FuturesEmaSignal is not null) components.Add(new(CacheComponentType.Ema, position));
        if (eligible.FuturesBbSignal is not null) components.Add(new(CacheComponentType.BollingerBand, position));
        if (eligible.FuturesTradeSignal is not null) components.Add(new(CacheComponentType.TradeSignal, position));

        if (components.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(ignoredReason))
                context.Logger.LogDebug(
                    "Ignored Market Outlook component {EventSource} for {EntityId}: {Reason}",
                    source.EventSource,
                    source.EntityId.Format(),
                    ignoredReason);
            return;
        }

        var now = DateTime.UtcNow;
        var result = Cache.Write(
            eligible.EntityId,
            components,
            state => MergeComponent(state, eligible),
            state => MarketOutlookComposer.Compose(state, MarketOutlookRefreshTrigger.Component, now));
        await NotifyAsync(result.Snapshot, source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId, source.EventSource, context).ConfigureAwait(false);
    }

    static MarketOutlookInputState MergeComponent(
        MarketOutlookInputState state,
        MarketOutlookComponentChangedRealtimeEvent source)
    {
        var iti = source.FuturesItiSignal;
        return state with
        {
            FuturesRsiSignal = source.FuturesRsiSignal ?? state.FuturesRsiSignal,
            FuturesTdiSignal = source.FuturesTdiSignal ?? state.FuturesTdiSignal,
            LatestItiTrendSignal = iti ?? state.LatestItiTrendSignal,
            TrendDirectionChange = iti?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged
                ? iti : state.TrendDirectionChange,
            TrendExtremeChange = iti?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
                ? iti : state.TrendExtremeChange,
            TrendReversalChange = iti?.IntrinsicTimeMode == IntrinsicTimeModeType.TrendReversalChanged
                ? iti : state.TrendReversalChange,
            VixFuturesPrice = source.VixFuturesPrice > 0 ? source.VixFuturesPrice : state.VixFuturesPrice,
            FuturesEmaSignal = source.FuturesEmaSignal ?? state.FuturesEmaSignal,
            FuturesBbSignal = source.FuturesBbSignal ?? state.FuturesBbSignal,
            FuturesTradeSignal = source.FuturesTradeSignal ?? state.FuturesTradeSignal
        };
    }

    static async ValueTask ApplyEodAsync(
        MarketOutlookEodUpdatedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (!string.Equals(source.FuturesEodData.Symbol, "ES", StringComparison.OrdinalIgnoreCase))
            return;
        var now = DateTime.UtcNow;
        var result = Cache.Write(
            source.EntityId,
            [new(CacheComponentType.Eod, Position(source.Id, source.EventId, source.ReceivedOn))],
            state => state with { FuturesEodData = source.FuturesEodData },
            state => MarketOutlookComposer.Compose(state, MarketOutlookRefreshTrigger.EodSession, now));
        await NotifyAsync(result.Snapshot, source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId, source.EventSource, context).ConfigureAwait(false);
    }

    static async ValueTask ApplyEsTradeAsync(
        FuturesMarketPriceUpdatedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (source.UpdateSource != FuturesMarketPriceUpdateSource.Trade
            || source.Price.Trade is not { } trade
            || !source.Price.ContractId.StartsWith("ES", StringComparison.OrdinalIgnoreCase)
            || trade.NormalizedTradeAction != NormalizedTradeAction.New
            || trade.LastPrice <= 0m
            || trade.LastSize == 0)
            return;

        var entityId = new MarketOutlookEntityId(source.Price.ContractId, source.Price.ValueDate);
        var position = new MarketOutlookSourcePosition(
            source.Id,
            source.EventId,
            trade.EventTimestamp.UtcDateTime,
            trade.StreamEpochId,
            trade.TradeOrdinal);
        MarketOutlookDailyPreviewCalculator.TryCalculate(source, out var liveEma, out var liveBb);
        var now = DateTime.UtcNow;
        var result = Cache.Write(
            entityId,
            [new(CacheComponentType.EsTrade, position)],
            state => state with
            {
                CurrentEsPrice = trade.LastPrice,
                FuturesEmaSignal = liveEma ?? state.FuturesEmaSignal,
                FuturesBbSignal = liveBb ?? state.FuturesBbSignal
            },
            state => MarketOutlookComposer.Compose(state, MarketOutlookRefreshTrigger.EsTrade, now));
        await NotifyAsync(result.Snapshot, source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId, "MarketOutlookEsTradeRefresh", context).ConfigureAwait(false);
    }

    static async ValueTask NotifyAsync(
        MarketOutlookReadModel projection,
        Guid commandId,
        string aggregateId,
        string eventSource,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        var entityId = new MarketOutlookEntityId(projection.ContractId, projection.ValueDate);
        var notification = new MarketOutlookUpdatedNotifyEvent
        {
            Subject = new ActorSubject(
                ActorType.Notify,
                MarketOutlookUpdatedNotifyEvent.Actor,
                MarketOutlookUpdatedNotifyEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = commandId == Guid.Empty ? Guid.NewGuid() : commandId,
            AggregateId = aggregateId,
            EventSource = eventSource,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = projection
        };
        try
        {
            await context.SendAsync<MarketOutlookUpdatedNotifyEvent, MarketOutlookEntityId>(notification)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Cache.RecordNotificationFailure();
            context.Logger.LogErrorEvent(
                ActorName,
                exception,
                "Market Outlook notification failed after cache commit for {EntityId}",
                entityId.Format());
        }
    }

    static MarketOutlookSourcePosition Position(Guid id, long sequence, DateTime timestamp) =>
        new(id, sequence, timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime());

    protected override ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(
            ActorName,
            exception,
            "Market Outlook hot-cache refresh failed for {EntityId}",
            @event.Subject.EntityId);
        return ValueTask.CompletedTask;
    }
}
