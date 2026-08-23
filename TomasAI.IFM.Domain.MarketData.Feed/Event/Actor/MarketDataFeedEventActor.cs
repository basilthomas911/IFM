using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Actor;

public class MarketDataFeedEventActor(IEventActorContext<MarketDataFeedEventActor> actorContext)
    : BaseEventActor<MarketDataFeedEventActor>(actorContext.Supervisor, actorContext.Logger, actorContext.ActorId)
{
    public const string Actor = "MarketDataFeedEvent";

    /// <summary>Gets the typed event context supplied at construction.</summary>
    protected IMarketDataFeedEventContext EventContext { get; } = IsArgumentNull.Set(actorContext as IMarketDataFeedEventContext, nameof(actorContext))!;
    readonly ILogger<MarketDataFeedEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    MarketDataFeedEventParameters _eventParameters = new(
        ((IMarketDataFeedEventContext)actorContext).MarketDataApi,
        ((IMarketDataFeedEventContext)actorContext).OptionTradeLiveFeedMap,
        ((IMarketDataFeedEventContext)actorContext).BlackboardService,
        ((IMarketDataFeedEventContext)actorContext).StatusConsoleWriter, actorContext.Logger);
    readonly Dictionary<string, Func<IEvent, IMarketDataFeedEventContext, IEventActorContext, IEventActorContext, MarketDataFeedEventParameters, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(MarketDataFeedStartedEvent).Name] = async (evt, ctx, _, eventApi, eventParams) =>
        {
            var e = (evt as MarketDataFeedStartedEvent)!;
            return await e.ExecuteAsync(ctx, eventApi, eventParams);
        },
        [typeof(MarketDataFeedStartedCompleteEvent).Name] = async (evt, ctx, commandApi, _, eventParams) =>
        {
            var e = (evt as MarketDataFeedStartedCompleteEvent)!;
            return await e.ExecuteAsync(ctx, commandApi, eventParams);
        },
        [typeof(MarketDataFeedStoppedEvent).Name] = async (evt, ctx, _, eventApi, eventParams) =>
        {
            var e = (evt as MarketDataFeedStoppedEvent)!;
            return await e.ExecuteAsync(ctx, eventApi, eventParams);
        },
        [typeof(MarketDataFeedStoppedCompleteEvent).Name] = async (evt, ctx, _, _, eventParams) =>
        {
            var e = (evt as MarketDataFeedStoppedCompleteEvent)!;
            return await e.ExecuteAsync(ctx, eventParams);
        },
        [typeof(MarketDataFeedResetEvent).Name] = async (evt, ctx, _, eventApi, eventParams) =>
        {
            var e = (evt as MarketDataFeedResetEvent)!;
            return await e.ExecuteAsync(ctx, eventApi, eventParams);
        },
        [typeof(MarketDataFeedResetCompleteEvent).Name] = async (evt, ctx, commandApi, eventApi, eventParams) =>
        {
            var e = (evt as MarketDataFeedResetCompleteEvent)!;
            return await e.ExecuteAsync(ctx, commandApi, eventApi, eventParams);
        }
    };

    protected override ValueTask OnStartup(IEventActorContext context)
    {
        _ = EventContext;
        _ = EventContext;
        return ValueTask.CompletedTask;
    }



    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not correspond to a known event or if the event cannot be
    /// resolved from the message.</exception>
    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        IsArgumentNull.Check(context);
        var msgSubject = message.Subject;
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
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [MarketDataFeedStartedEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStartedEvent>()!,
        [MarketDataFeedStartedCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStartedCompleteEvent>()!,
        [MarketDataFeedStoppedEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStoppedEvent>()!,
        [MarketDataFeedStoppedCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedStoppedCompleteEvent>()!,
        [MarketDataFeedResetEvent.Verb] = msg => msg.AsEvent<MarketDataFeedResetEvent>()!,
        [MarketDataFeedResetCompleteEvent.Verb] = msg => msg.AsEvent<MarketDataFeedResetCompleteEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Provides access to actor state and services required
    /// for event handling. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type, or if the event cannot be resolved from the message.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, EventContext, EventContext, EventContext, _eventParameters);
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
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
        }
        catch (Exception innerEx)
        {
            await innerEx.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context);
            _logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }

}
