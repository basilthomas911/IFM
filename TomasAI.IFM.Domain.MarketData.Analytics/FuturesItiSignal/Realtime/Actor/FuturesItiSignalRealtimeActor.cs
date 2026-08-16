using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;

/// <summary>
/// Receives routed futures market-price updates and bridges eligible ES updates
/// into the durable futures ITI command workflow.
/// </summary>
/// <param name="supervisor">The actor supervisor that owns the realtime mailbox.</param>
/// <param name="commandApiFactory">Creates the durable command API bound to this actor context.</param>
/// <param name="marketDataApi">Provides current-contract and live-price state.</param>
/// <param name="logger">The typed logger used by this actor and its handler.</param>
public class FuturesItiSignalRealtimeActor(
    IActorSupervisor supervisor,
    IActorMarketDataAnalyticsCommandApiFactory commandApiFactory,
    IMarketDataApi marketDataApi,
    IDbContextFactory dbFactory,
    ILogger<FuturesItiSignalRealtimeActor> logger)
    : BaseEventActor<FuturesItiSignalRealtimeActor>(
        supervisor,
        logger,
        new ActorMailboxId(ActorType.Realtime, ActorName))
{
    /// <summary>Identifies the futures ITI realtime actor mailbox.</summary>
    public const string ActorName = "FuturesItiSignal";

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    /// <summary>Maps supported realtime verbs to MessagePack event parsers.</summary>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!
    };

    IActorMarketDataAnalyticsCommandApi? _commandApi;
    readonly FuturesItiSignalStreamOwnership _streamOwnership = new();
    readonly FuturesItiSignalRealtimeState _realtimeState = new(dbFactory);

    /// <summary>Maps supported event types to their domain extension handlers.</summary>
    readonly Dictionary<string, Func<
        IEvent,
        IEventActorContext,
        IActorMarketDataAnalyticsCommandApi,
        FuturesItiSignalStreamOwnership,
        FuturesItiSignalRealtimeState,
        ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesMarketPriceUpdatedRealtimeEvent).Name] =
            (@event, context, commandApi, streamOwnership, realtimeState) => ((FuturesMarketPriceUpdatedRealtimeEvent)@event)
                .ExecuteAsync(
                    context,
                    commandApi,
                    marketDataApi,
                    streamOwnership,
                    realtimeState,
                    logger)
    };

    /// <summary>Registers the route from the primary market-price actor.</summary>
    protected override ValueTask OnStartup(IEventActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ = GetCommandApi(context);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>Removes the route from the primary market-price actor.</summary>
    protected override async ValueTask OnShutdown(IEventActorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        await _streamOwnership.ReleaseAsync(marketDataApi).ConfigureAwait(false);
    }

    IActorMarketDataAnalyticsCommandApi GetCommandApi(IEventActorContext context) =>
        _commandApi ??= commandApiFactory.Create(context);

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

        if (!_receiveMap.TryGetValue(@event.GetType().Name, out var handler))
        {
            throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }

        _ = await handler(
                @event,
                context,
                GetCommandApi(context),
                _streamOwnership,
                _realtimeState)
            .ConfigureAwait(false);
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
