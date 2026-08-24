using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using NATS.Client.Core;
using global::TomasAI.IFM.Shared.EventModelActor;
using global::TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Actor;

public class FuturesOptionTickDataEventActor(IEventActorContext<FuturesOptionTickDataEventActor> actorContext)
    : BaseEventActor<FuturesOptionTickDataEventActor>(actorContext, actorContext.Logger)
{
    public const string Actor = "FuturesOptionTickDataEvent";

    /// <summary>Gets the typed event context supplied at construction.</summary>
    protected IFuturesOptionTickDataEventContext EventContext { get; } = IsArgumentNull.Set(actorContext as IFuturesOptionTickDataEventContext, nameof(actorContext))!;
    readonly ILogger<FuturesOptionTickDataEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly FuturesOptionTickDataEventParameters _eventParameters = new(
        ((IFuturesOptionTickDataEventContext)actorContext).MarketDataApi, ((IFuturesOptionTickDataEventContext)actorContext).StatusConsoleWriter, actorContext.Logger);
    readonly Dictionary<string, Func<IEvent, IFuturesOptionTickDataEventContext, IEventActorContext, FuturesOptionTickDataEventParameters, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesOptionTickDataStreamingStartedEvent).Name] = async (evt, context, eventApi, eventParams) =>
        {
            var e = (evt as FuturesOptionTickDataStreamingStartedEvent)!;
            return await e.ExecuteAsync(context, eventApi, eventParams);
        },
        [typeof(FuturesOptionTickDataStreamingStoppedEvent).Name] = async (evt, context, eventApi, eventParams) =>
        {
            var e = (evt as FuturesOptionTickDataStreamingStoppedEvent)!;
            return await e.ExecuteAsync(context, eventApi, eventParams);
        }
    };

    protected override ValueTask OnStartup(IEventActorContext<FuturesOptionTickDataEventActor> context)
    {
        _ = EventContext;
        return ValueTask.CompletedTask;
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesOptionTickDataEventActor> context)
    {
        foreach (var registration in _eventParameters.Streams.Drain())
        {
            try
            {
                await _eventParameters.MarketDataApi.StopStreamingFuturesOptionTickDataAsync(
                    registration.Key.ContractId,
                    registration.Key.Owner).ConfigureAwait(false);
            }
            catch (TomasAI.IFM.Application.MarketData.Contracts.MarketDataApiNotRunningException)
            {
                // The host may stop the transient market-data epoch before actor teardown.
                // Draining the actor-owned registration is already complete in that case.
            }
        }
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
    protected override IEvent ParseMessage(IEventActorContext<FuturesOptionTickDataEventActor> context, IActorMessage message)
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
        [FuturesOptionTickDataStreamingStartedEvent.Verb] = msg => msg.AsEvent<FuturesOptionTickDataStreamingStartedEvent>()!,
        [FuturesOptionTickDataStreamingStoppedEvent.Verb] = msg => msg.AsEvent<FuturesOptionTickDataStreamingStoppedEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Provides access to actor state and services required
    /// for event handling. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type, or if the event cannot be resolved from the message.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesOptionTickDataEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(
            @event,
            EventContext,
            EventContext,
            _eventParameters);
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
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesOptionTickDataEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
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
            _logger.LogErrorEvent(Actor, innerEx, "Failed to send EventExceptionEvent for: {ThreadId}", threadId);
        }
    }
}
