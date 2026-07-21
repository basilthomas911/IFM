using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

/// <summary>
/// Represents an event actor responsible for processing futures tick data events within the actor system. Provides
/// mechanisms for parsing incoming messages, handling event execution, managing actor state, and reporting errors
/// specific to futures tick data events.
/// </summary>
/// <param name="supervisor">The actor supervisor that manages actor lifecycle and coordinates event processing within the system. Cannot be
/// null.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the futures tick data event actor. Cannot be null.</param>
public class FuturesTickDataEventActor(
    IActorSupervisor supervisor,
    IMarketDataApi marketDataApi,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesTickDataEventActor> logger)
    : BaseEventActor<FuturesTickDataEventActor>(supervisor, logger, new ActorMailboxId(ActorType.Event, Actor))
{
    public const string Actor = "FuturesTickDataEvent";
    readonly FuturesTickDataEventParameters _eventParameters = new(
        marketDataApi, blackboardService, statusConsoleWriter, logger);
    readonly Dictionary<string, Func<IEvent, IEventActorContext, FuturesTickDataEventParameters, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTickDataInsertedEvent).Name] = async (evt, context, eventParams) =>
        {
            var e = (evt as FuturesTickDataInsertedEvent)!;
            return await e.ExecuteAsync(context, eventParams);
        },
        [typeof(FuturesTickDataStreamingStartedEvent).Name] = async (evt, context, eventParams) =>
        {
            var e = (evt as FuturesTickDataStreamingStartedEvent)!;
            return await e.ExecuteAsync(context, eventParams);
        },
        [typeof(FuturesTickDataStreamingStoppedEvent).Name] = async (evt, context, eventParams) =>
        {
            var e = (evt as FuturesTickDataStreamingStoppedEvent)!;
            return await e.ExecuteAsync(context, eventParams);
        }
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known event or if the event cannot be
    /// resolved from the message.</exception>
    protected override IEvent ParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject.ToSubject();
        if (msgSubject is not { ActorType: ActorType.Event, Name: Actor }
            || !_parseMap.TryGetValue(msgSubject.Verb, out var messageParser))
            return default!;
        var @event = messageParser.Invoke(message);
        IsArgumentNull.Check(@event);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Maps event verb strings to factory functions that convert NATS messages into corresponding event instances.
    /// </summary>
    static readonly Dictionary<string, Func<NatsMsg<byte[]>, IEvent>> _parseMap = new()
    {
        [FuturesTickDataInsertedEvent.Verb] = msg => msg.AsEvent<FuturesTickDataInsertedEvent>()!,
        [FuturesTickDataStreamingStartedEvent.Verb] = msg => msg.AsEvent<FuturesTickDataStreamingStartedEvent>()!,
        [FuturesTickDataStreamingStoppedEvent.Verb] = msg => msg.AsEvent<FuturesTickDataStreamingStoppedEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Provides access to actor state and services required
    /// for event handling. Cannot be null.</param>
    /// <param name="state">The current state of the actor, used to process the event. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type, or if the event cannot be resolved from the message.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(state);
        IsArgumentNull.Check(@event);
        var futuresTickDataEventState = IsArgumentNull.Set((state as FuturesTickDataEventState)!);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, context, _eventParameters);
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred. Provides access to actor state and metadata relevant to
    /// error handling.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised. Used to associate the error with the correct
    /// execution context.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing. Contains information about the error to be reported.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            IsArgumentNull.Check(threadId);
            IsArgumentNull.Check(@event);
            await ex.SendErrorEventAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }

    /// <summary>
    /// Asynchronously loads the actor state for the specified event actor context and thread.
    /// </summary>
    /// <param name="context">The event actor context used to resolve the actor state. Cannot be null.</param>
    /// <param name="threadId">The identifier of the actor thread for which the state is being loaded.</param>
    /// <param name="event">The event for which state is being loaded. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event)
    {
        var actorState = IsArgumentNull.Set(context.Container.Resolve<IEventActorState<FuturesTickDataEventState>>());
        actorState.Id = threadId;
        return await ValueTask.FromResult(actorState);
    }
}
