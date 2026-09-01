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
/// Owns the process-local, versionless Market Outlook projection. Every eligible component and
/// every accepted ES last trade replaces the immutable current value and independently notifies UI clients.
/// </summary>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the stable actor mailbox name retained for wire compatibility.</summary>
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
        var eligible = MarketOutlookComponentEligibility.SelectEligible(source, out var rejectedReason);
        var position = Position(source.Id, source.EventId, source.ReceivedOn);
        var accepted = false;
        accepted |= Update(eligible.EntityId, CacheComponentType.Rsi, position,
            eligible.FuturesRsiSignal, static (state, value) => state with { FuturesRsiSignal = value });
        accepted |= Update(eligible.EntityId, CacheComponentType.Tdi, position,
            eligible.FuturesTdiSignal, static (state, value) => state with { FuturesTdiSignal = value });
        accepted |= UpdateIti(eligible, position);
        if (eligible.VixFuturesPrice > 0)
            accepted |= Cache.TryUpdateInput(eligible.EntityId, CacheComponentType.Vx, position,
                state => state with { VixFuturesPrice = eligible.VixFuturesPrice }, out _);
        accepted |= Update(eligible.EntityId, CacheComponentType.Ema, position,
            eligible.FuturesEmaSignal, static (state, value) => state with { FuturesEmaSignal = value });
        accepted |= Update(eligible.EntityId, CacheComponentType.BollingerBand, position,
            eligible.FuturesBbSignal, static (state, value) => state with { FuturesBbSignal = value });
        accepted |= Update(eligible.EntityId, CacheComponentType.TradeSignal, position,
            eligible.FuturesTradeSignal, static (state, value) => state with { FuturesTradeSignal = value });

        if (!accepted)
        {
            if (!string.IsNullOrWhiteSpace(rejectedReason))
                context.Logger.LogDebug(
                    "Ignored Market Outlook component {EventSource} for {EntityId}: {Reason}",
                    source.EventSource,
                    source.EntityId.Format(),
                    rejectedReason);
            return;
        }
        await ComposeAndNotifyAsync(
            eligible.EntityId,
            MarketOutlookRefreshTrigger.Component,
            source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId,
            source.EventSource,
            context).ConfigureAwait(false);
    }

    static async ValueTask ApplyEodAsync(
        MarketOutlookEodUpdatedRealtimeEvent source,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context)
    {
        if (!string.Equals(source.FuturesEodData.Symbol, "ES", StringComparison.OrdinalIgnoreCase))
            return;
        if (!Cache.TryUpdateInput(
                source.EntityId,
                CacheComponentType.Eod,
                Position(source.Id, source.EventId, source.ReceivedOn),
                state => state with { FuturesEodData = source.FuturesEodData },
                out _))
            return;
        await ComposeAndNotifyAsync(
            source.EntityId,
            MarketOutlookRefreshTrigger.EodSession,
            source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId,
            source.EventSource,
            context).ConfigureAwait(false);
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
            || trade.LastSize == 0
            || trade.StreamEpochId == Guid.Empty
            || trade.TradeOrdinal <= 0)
            return;

        var entityId = new MarketOutlookEntityId(source.Price.ContractId, source.Price.ValueDate);
        var position = new MarketOutlookSourcePosition(
            source.Id,
            source.EventId,
            trade.EventTimestamp.UtcDateTime,
            trade.StreamEpochId,
            trade.TradeOrdinal);
        MarketOutlookDailyPreviewCalculator.TryCalculate(source, out var liveEma, out var liveBb);
        if (!Cache.TryUpdateInput(
                entityId,
                CacheComponentType.EsTrade,
                position,
                state => state with
                {
                    CurrentEsPrice = trade.LastPrice,
                    FuturesEmaSignal = liveEma ?? state.FuturesEmaSignal,
                    FuturesBbSignal = liveBb ?? state.FuturesBbSignal
                },
                out _))
            return;
        await ComposeAndNotifyAsync(
            entityId,
            MarketOutlookRefreshTrigger.EsTrade,
            source.CommandId == Guid.Empty ? source.Id : source.CommandId,
            source.AggregateId,
            "MarketOutlookEsTradeRefresh",
            context,
            liveEma,
            liveBb).ConfigureAwait(false);
    }

    static bool Update<T>(
        MarketOutlookEntityId entityId,
        CacheComponentType component,
        MarketOutlookSourcePosition position,
        T? value,
        Func<MarketOutlookInputState, T, MarketOutlookInputState> update)
        where T : class
        => value is not null
            && Cache.TryUpdateInput(entityId, component, position, state => update(state, value), out _);

    static bool UpdateIti(
        MarketOutlookComponentChangedRealtimeEvent source,
        MarketOutlookSourcePosition position)
    {
        if (source.FuturesItiSignal is not { } iti)
            return false;
        var milestoneAccepted = iti.IntrinsicTimeMode switch
        {
            IntrinsicTimeModeType.TrendDirectionChanged => Cache.TryUpdateInput(
                source.EntityId, CacheComponentType.ItiDirection, position,
                state => state with { TrendDirectionChange = iti }, out _),
            IntrinsicTimeModeType.TrendExtremeChanged => Cache.TryUpdateInput(
                source.EntityId, CacheComponentType.ItiExtreme, position,
                state => state with { TrendExtremeChange = iti }, out _),
            IntrinsicTimeModeType.TrendReversalChanged => Cache.TryUpdateInput(
                source.EntityId, CacheComponentType.ItiReversal, position,
                state => state with { TrendReversalChange = iti }, out _),
            _ => false
        };
        var latestAccepted = Cache.TryUpdateInput(
            source.EntityId,
            CacheComponentType.ItiLatest,
            position,
            state => state with { LatestItiTrendSignal = iti },
            out _);
        return milestoneAccepted || latestAccepted;
    }

    static async ValueTask ComposeAndNotifyAsync(
        MarketOutlookEntityId entityId,
        MarketOutlookRefreshTrigger trigger,
        Guid commandId,
        string aggregateId,
        string eventSource,
        IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> context,
        FuturesEmaSignalReadModel? liveEma = null,
        FuturesBbSignalReadModel? liveBb = null)
    {
        if (!Cache.TryGetInputs(entityId, out var state))
            return;
        var projection = MarketOutlookComposer.Compose(state, trigger, DateTime.UtcNow, liveEma, liveBb);
        Cache.SetCurrent(projection);
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
