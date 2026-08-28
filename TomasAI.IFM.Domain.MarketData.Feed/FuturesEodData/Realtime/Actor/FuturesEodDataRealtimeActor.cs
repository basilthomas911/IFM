using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;
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
    protected IFuturesEodDataRealtimeContext RealtimeContext { get; } = IsArgumentNull.Set(actorContext as IFuturesEodDataRealtimeContext, nameof(actorContext))!;

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

    readonly FuturesEodDataEventParameters _parameters = new(
        ((IFuturesEodDataRealtimeContext)actorContext).BlackboardService,
        ((IFuturesEodDataRealtimeContext)actorContext).StatusConsoleWriter,
        actorContext.Logger);

    static readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext,
        FuturesEodDataEventParameters, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesEodDataRealtimeContext,
            FuturesEodDataEventParameters, ValueTask>>
        {
            [typeof(FuturesTickTradeDataInsertedEvent)] = async (@event, context, parameters) =>
            {
                _ = await ((FuturesTickTradeDataInsertedEvent)@event).ExecuteAsync(
                        context,
                        context.MarketDataApi,
                        context.BlackboardService,
                        context.StatusConsoleWriter,
                        context.Projector,
                        context.Logger)
                    .ConfigureAwait(false);
            },
            [typeof(FuturesMarketPriceUpdatedRealtimeEvent)] = async (@event, context, parameters) =>
            {
                _ = await ((FuturesMarketPriceUpdatedRealtimeEvent)@event).ExecuteVxQuoteAsync(
                        context.MarketDataApi,
                        context.Projector,
                        context.StatusConsoleWriter,
                        context.Logger)
                    .ConfigureAwait(false);
            },
            [typeof(FuturesSessionStatisticsUpdatedRealtimeEvent)] = async (@event, context, parameters) =>
            {
                _ = await ((FuturesSessionStatisticsUpdatedRealtimeEvent)@event)
                    .ExecuteAsync(context, context.Projector, context.Logger).ConfigureAwait(false);
            },
            [typeof(FuturesEodDataInsertedEvent)] = static (@event, context, _) =>
            {
                var inserted = (FuturesEodDataInsertedEvent)@event;
                context.BlackboardService.MarketDataFeed.FuturesEodData.Set(
                    inserted.FuturesEodData.ContractId,
                    inserted.FuturesEodData.ValueDate,
                    inserted.FuturesEodData);
                return ValueTask.CompletedTask;
            },
            [typeof(FuturesEodDataInsertedCompleteEvent)] = async (@event, context, parameters) =>
            {
                _ = await ((FuturesEodDataInsertedCompleteEvent)@event)
                    .ExecuteAsync(context, context, parameters).ConfigureAwait(false);
            },
            [typeof(VixFuturesEodDataInsertedCompleteEvent)] = async (@event, context, parameters) =>
            {
                _ = await ((VixFuturesEodDataInsertedCompleteEvent)@event)
                    .ExecuteAsync(context, parameters).ConfigureAwait(false);
            },
            [typeof(FuturesEodDataInsertedFailEvent)] = static (@event, context, _) =>
            {
                LogProjectionFailure((FuturesEodDataInsertedFailEvent)@event, context.Logger);
                return ValueTask.CompletedTask;
            },
            [typeof(VixFuturesEodDataInsertedFailEvent)] = static (@event, context, _) =>
            {
                LogProjectionFailure((VixFuturesEodDataInsertedFailEvent)@event, context.Logger);
                return ValueTask.CompletedTask;
            },
            [typeof(VixFuturesEodDataInsertedEvent)] = static (_, _, _) => ValueTask.CompletedTask,
            [typeof(FuturesEodSessionStatisticsUpdatedEvent)] = static (_, _, _) => ValueTask.CompletedTask
        };

    protected override async ValueTask OnStartup(IEventActorContext<FuturesEodDataRealtimeActor> context)
    {
        await ((IFuturesEodDataRealtimeContext)actorContext).Projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(TickTradeRoute, Id);
        context.AddRealtimeRouter(MarketPriceRoute, Id);
        context.AddRealtimeRouter(SessionStatisticsRoute, Id);
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesEodDataRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(TickTradeRoute, Id);
        context.RemoveRealtimeRouter(MarketPriceRoute, Id);
        context.RemoveRealtimeRouter(SessionStatisticsRoute, Id);
        await ((IFuturesEodDataRealtimeContext)actorContext).Projector.StopAsync().ConfigureAwait(false);
    }

    protected override IEvent ParseMessage(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(domainEvent, _receiveMap);
        await handler(domainEvent, RealtimeContext, _parameters).ConfigureAwait(false);
    }

    static void LogProjectionFailure(IErrorEvent failed, ILogger logger) => logger.LogErrorEvent(
        ActorName,
        "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
        failed.EventName,
        failed.Subject.EntityId,
        failed.ErrorMessage);

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesEodDataRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
