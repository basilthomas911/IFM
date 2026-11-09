using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;

/// <summary>
/// Owns the rolling EOD branch of the live futures feed. It consumes routed
/// TickAggregation observations and publishes source/complete/fail over Core
/// NATS without durable replay.
/// </summary>
public class FuturesEodDataRealtimeActor(IRealtimeActorContext<FuturesEodDataRealtimeActor> actorContext)
    : BaseEventActor<FuturesEodDataRealtimeActor>(actorContext, actorContext.Logger)
{
    public const string ActorName = FuturesEodDataInsertedEvent.Actor;

    /// <summary>Gets the typed realtime context supplied at construction.</summary>
    protected IFuturesEodDataRealtimeContext RealtimeContext { get; } = IsArgumentNull.Named(actorContext as IFuturesEodDataRealtimeContext, nameof(actorContext))!;

    static readonly ActorTypeId TickTradeRoute = new(
        ActorType.Realtime,
        FuturesTickTradeDataInsertedEvent.Actor,
        FuturesTickTradeDataInsertedEvent.Verb);

    static readonly ActorTypeId MarketPriceRoute = new(
        ActorType.Realtime,
        FuturesMarketPriceUpdatedRealtimeEvent.Actor,
        FuturesMarketPriceUpdatedRealtimeEvent.Verb);

    static readonly ActorTypeId SessionStatisticsRoute = new(
        ActorType.Realtime,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Actor,
        FuturesSessionStatisticsUpdatedRealtimeEvent.Verb);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesTickTradeDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesTickTradeDataInsertedEvent>()!,
            [FuturesMarketPriceUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>()!,
            [FuturesSessionStatisticsUpdatedRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesSessionStatisticsUpdatedRealtimeEvent>()!,
            [FuturesEodSessionStatisticsUpdatedEvent.Verb] =
            message => message.AsEvent<FuturesEodSessionStatisticsUpdatedEvent>()!,
            [FuturesEodDataInsertedEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedEvent>()!,
            [FuturesEodDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedCompleteEvent>()!,
            [FuturesEodDataInsertedFailEvent.Verb] =
            message => message.AsEvent<FuturesEodDataInsertedFailEvent>()!,
            [VixFuturesEodDataInsertedEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedEvent>()!,
            [VixFuturesEodDataInsertedCompleteEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedCompleteEvent>()!,
            [VixFuturesEodDataInsertedFailEvent.Verb] =
            message => message.AsEvent<VixFuturesEodDataInsertedFailEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext, IEventActorContext<FuturesEodDataRealtimeActor>,
        FuturesEodDataEventParameters, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext, IEventActorContext<FuturesEodDataRealtimeActor>,
            FuturesEodDataEventParameters, ValueTask>>
        {
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = static (@event, context, _, _) =>
                ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteAsync(context),
            [typeof(FuturesTickTradeDataInsertedEvent)] = static (@event, context, actorContext, _) =>
                context.ReceiveTradeAsync(actorContext, (FuturesTickTradeDataInsertedEvent)@event),
            [typeof(FuturesSessionStatisticsUpdatedRealtimeEvent)] = static (@event, context, actorContext, _) =>
                context.DispatchStatisticsAsync(actorContext, (FuturesSessionStatisticsUpdatedRealtimeEvent)@event),
            [typeof(FuturesEodDataInsertedEvent)] = static (@event, context, _, _) =>
                ((FuturesEodDataInsertedEvent)@event).ExecuteAsync(context),
            [typeof(FuturesEodDataInsertedCompleteEvent)] = static (@event, context, _, parameters) =>
                ((FuturesEodDataInsertedCompleteEvent)@event).ExecuteAsync(context, parameters),
            [typeof(VixFuturesEodDataInsertedCompleteEvent)] = static (@event, context, _, parameters) =>
                ((VixFuturesEodDataInsertedCompleteEvent)@event).ExecuteAsync(context, parameters),
            [typeof(FuturesEodDataInsertedFailEvent)] = static (@event, context, _, _) =>
                ((FuturesEodDataInsertedFailEvent)@event).ExecuteAsync(context),
            [typeof(VixFuturesEodDataInsertedFailEvent)] = static (@event, context, _, _) =>
                ((VixFuturesEodDataInsertedFailEvent)@event).ExecuteAsync(context),
            [typeof(VixFuturesEodDataInsertedEvent)] = static (@event, context, _, _) =>
                ((VixFuturesEodDataInsertedEvent)@event).ExecuteAsync(context),
            [typeof(FuturesEodSessionStatisticsUpdatedEvent)] = static (@event, context, _, _) =>
                ((FuturesEodSessionStatisticsUpdatedEvent)@event).ExecuteAsync(context),
            [typeof(FuturesEodDataUpdatedEvent)] = static (@event, context, _, _) =>
                context.InvalidateUpdateAsync((FuturesEodDataUpdatedEvent)@event)
        };

    readonly FuturesEodDataEventParameters _parameters = new(
        ((IFuturesEodDataRealtimeContext)actorContext).StatusConsoleWriter,
        actorContext.Logger);

    protected override async ValueTask OnStartup(IEventActorContext<FuturesEodDataRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await ((IFuturesEodDataRealtimeContext)actorContext).Projector.StartAsync(context).ConfigureAwait(false);
        RealtimeContext.StartTradeWorkers();
        context.AddRealtimeRouter(TickTradeRoute, Id);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        context.AddRealtimeRouter(SessionStatisticsRoute, Id);
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesEodDataRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        context.RemoveRealtimeRouter(SessionStatisticsRoute, Id);
        try
        {
            await RealtimeContext.StopTradeWorkersAsync().ConfigureAwait(false);
        }
        finally
        {
            await ((IFuturesEodDataRealtimeContext)actorContext).Projector.StopAsync().ConfigureAwait(false);
        }
    }

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        IActorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        // Both EOD notifications and routed market prices use "Updated". The
        // destination subject identifies this mailbox; the source identifies
        // the payload contract to deserialize.
        if (message.Subject.ActorType == ActorType.Realtime
            && string.Equals(message.Subject.Name, ActorName, StringComparison.Ordinal)
            && string.Equals(message.Subject.Verb, FuturesEodDataUpdatedEvent.Verb, StringComparison.Ordinal)
            && string.Equals(message.SourceSubject.Name, FuturesEodDataUpdatedEvent.Actor, StringComparison.Ordinal))
            return message.AsEvent<FuturesEodDataUpdatedEvent>()!;
        return ParseMappedRealtimeEvent(context, message, _parseMap);
    }

    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        IEvent @event) =>
        ResolveMappedEventHandler(@event, _receiveMap)(@event, RealtimeContext, context, _parameters);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception ex) =>
        await ex.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
