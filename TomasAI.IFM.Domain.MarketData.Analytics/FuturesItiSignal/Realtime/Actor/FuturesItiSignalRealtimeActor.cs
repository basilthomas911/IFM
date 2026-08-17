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
    IActorSupervisor supervisor,
    IRealtimeProjector<FuturesItiSignalRealtimeActor> projector,
    IMarketDataApi marketDataApi,
    IDbContextFactory dbFactory,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesItiSignalRealtimeActor> logger)
    : BaseEventActor<FuturesItiSignalRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    /// <summary>Identifies the futures ITI realtime actor mailbox.</summary>
    public const string ActorName = "FuturesItiSignal";
    public const string TradeSignalUpdatedVerb = "TradeSignalUpdated";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    /// <summary>Maps supported realtime verbs to MessagePack event parsers.</summary>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
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
    readonly FuturesItiSignalRealtimeState _realtimeState = new(dbFactory);

    /// <summary>Registers the route from the primary market-price actor.</summary>
    protected override async ValueTask OnStartup(IEventActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
    }

    /// <summary>Removes the route from the primary market-price actor.</summary>
    protected override async ValueTask OnShutdown(IEventActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await projector.StopAsync().ConfigureAwait(false);
        await _streamOwnership.ReleaseAsync(marketDataApi).ConfigureAwait(false);
    }

    /// <summary>Parses a routed market-price event addressed to this actor.</summary>
    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(message);

        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !_parseMap.TryGetValue(subject.Verb, out var parser))
            return default!;

        var @event = parser(message);
        ArgumentNullException.ThrowIfNull(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>Dispatches the parsed realtime event to its mapped handler.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(@event);

        switch (@event)
        {
            case FuturesMarketPriceUpdatedRealtimeEvent priceUpdated:
                _ = await priceUpdated.ExecuteAsync(
                        context,
                        projector,
                        marketDataApi,
                        _streamOwnership,
                        _realtimeState,
                        logger)
                    .ConfigureAwait(false);
                break;
            case FuturesItiSignalGeneratedCompleteEvent completed:
                _ = await completed.ExecuteRealtimeAsync(
                        context,
                        projector,
                        statusConsoleWriter,
                        logger)
                    .ConfigureAwait(false);
                break;
            case FuturesItiSignalGeneratedFailEvent failed:
                logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName,
                    failed.EntityId,
                    failed.ErrorMessage);
                break;
            case FuturesItiSignalGeneratedEvent:
                break;
            case FuturesTradeSignalUpdatedCompleteEvent tradeCompleted:
                _ = await tradeCompleted.ExecuteAsync(
                        context,
                        statusConsoleWriter,
                        logger)
                    .ConfigureAwait(false);
                break;
            case FuturesTradeSignalUpdatedFailEvent tradeFailed:
                logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    tradeFailed.EventName,
                    tradeFailed.EntityId,
                    tradeFailed.ErrorMessage);
                break;
            case FuturesTradeSignalUpdatedEvent:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }
    }

    /// <summary>Publishes the standard actor event error when realtime handling fails.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
