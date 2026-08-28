using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>
/// Receives routed futures market-price updates and bridges eligible ES updates
/// into the non-replayable futures ITI projection workflow.
/// </summary>
/// <param name="supervisor">The actor supervisor that owns the realtime mailbox.</param>
/// <param name="projector">Owns one-attempt realtime ITI and compatibility projections.</param>
/// <param name="marketDataApi">Provides current-contract and live-price state.</param>
/// <param name="dbFactory">Hydrates the actor-owned timeframe hot state at startup boundaries.</param>
/// <param name="statusConsoleWriter">Reports compatibility-flow errors to external status observers.</param>
/// <param name="logger">The typed logger used by this actor and its handler.</param>
public class FuturesItiSignalRealtimeActor(
    IRealtimeActorContext<FuturesItiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesItiSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesItiSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesItiSignalRealtimeContext, nameof(actorContext))!;

    /// <summary>Identifies the futures ITI realtime actor mailbox.</summary>
    public const string ActorName = FuturesItiSignalGeneratedEvent.RealtimeActor;
    public const string TradeSignalUpdatedVerb = "TradeSignalUpdated";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    /// <summary>Maps supported realtime verbs to MessagePack event parsers.</summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
        [FuturesItiSignalGeneratedEvent.Verb] =
            message => message.AsEvent<FuturesItiSignalGeneratedEvent>()!,
        [FuturesItiSignalGeneratedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesItiSignalGeneratedCompleteEvent>()!,
        [FuturesItiSignalGeneratedFailEvent.Verb] =
            message => message.AsEvent<FuturesItiSignalGeneratedFailEvent>()!,
        [TradeSignalUpdatedVerb] =
            message => message.AsEvent<FuturesTradeSignalUpdatedEvent>()!,
        [FuturesTradeSignalUpdatedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesTradeSignalUpdatedCompleteEvent>()!,
        [FuturesTradeSignalUpdatedFailEvent.Verb] =
            message => message.AsEvent<FuturesTradeSignalUpdatedFailEvent>()!
    };

    readonly FuturesItiSignalStreamOwnership _streamOwnership = new();
    readonly FuturesItiSignalRealtimeState _realtimeState = new(actorContext.DbFactory);

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IEventActorContext<FuturesItiSignalRealtimeActor>,
        IFuturesItiSignalRealtimeContext,
        FuturesItiSignalStreamOwnership,
        FuturesItiSignalRealtimeState,
        ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IEventActorContext<FuturesItiSignalRealtimeActor>,
            IFuturesItiSignalRealtimeContext,
            FuturesItiSignalStreamOwnership,
            FuturesItiSignalRealtimeState,
            ValueTask>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = async (
                @event, eventContext, context, ownership, state) =>
            {
                _ = await ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(
                        eventContext,
                        context.Projector,
                        context.MarketDataApi,
                        ownership,
                        state,
                        context.Logger)
                    .ConfigureAwait(false);
            },
            [typeof(FuturesItiSignalGeneratedCompleteEvent)] = async (
                @event, eventContext, context, ownership, state) =>
            {
                _ = await ((FuturesItiSignalGeneratedCompleteEvent)@event).ExecuteRealtimeAsync(
                        eventContext,
                        context.Projector,
                        context.StatusConsoleWriter,
                        context.Logger)
                    .ConfigureAwait(false);
            },
            [typeof(FuturesItiSignalGeneratedFailEvent)] = static (
                @event, eventContext, context, ownership, state) =>
            {
                var failed = (FuturesItiSignalGeneratedFailEvent)@event;
                context.Logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName,
                    failed.EntityId,
                    failed.ErrorMessage);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesItiSignalGeneratedEvent)] = static (
                @event, eventContext, context, ownership, state) => ValueTask.CompletedTask,
            [typeof(FuturesTradeSignalUpdatedCompleteEvent)] = async (
                @event, eventContext, context, ownership, state) =>
            {
                _ = await ((FuturesTradeSignalUpdatedCompleteEvent)@event).ExecuteAsync(
                        eventContext,
                        context.StatusConsoleWriter,
                        context.Logger)
                    .ConfigureAwait(false);
            },
            [typeof(FuturesTradeSignalUpdatedFailEvent)] = static (
                @event, eventContext, context, ownership, state) =>
            {
                var failed = (FuturesTradeSignalUpdatedFailEvent)@event;
                context.Logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName,
                    failed.EntityId,
                    failed.ErrorMessage);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesTradeSignalUpdatedEvent)] = static (
                @event, eventContext, context, ownership, state) => ValueTask.CompletedTask
        };

    /// <summary>Registers the route from the primary market-price actor.</summary>
    protected override async ValueTask OnStartup(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await actorContext.Projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
    }

    /// <summary>Removes the route from the primary market-price actor.</summary>
    protected override async ValueTask OnShutdown(IEventActorContext<FuturesItiSignalRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await actorContext.Projector.StopAsync().ConfigureAwait(false);
        await _streamOwnership.ReleaseAsync(actorContext.MarketDataApi).ConfigureAwait(false);
    }

    /// <summary>Parses a routed market-price event addressed to this actor.</summary>
    protected override IEvent ParseMessage(IEventActorContext<FuturesItiSignalRealtimeActor> context, IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <summary>Dispatches the parsed realtime event to its mapped handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        await handler(
                @event,
                context,
                ActorContext,
                _streamOwnership,
                _realtimeState)
            .ConfigureAwait(false);
    }

    /// <summary>Publishes the standard actor event error when realtime handling fails.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesItiSignalRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
