using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;

/// <summary>
///
/// </summary>
/// <param name="supervisor"></param>
/// <param name="statusConsoleWriter"></param>
/// <param name="logger"></param>
public class FuturesTdiSignalEventActor(
    IEventActorContext<FuturesTdiSignalEventActor> actorContext)
    : BaseEventActor<FuturesTdiSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesTdiSignalEventContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesTdiSignalEventContext, nameof(Context))!;

    public const string Actor = "FuturesTdiSignalEvent";
    static readonly ActorTypeId RsiSignalsRoute = new(
        ActorType.Event,
        FuturesRsiSignalsGeneratedEvent.Actor,
        FuturesRsiSignalsGeneratedEvent.Verb);

    readonly Dictionary<string, Func<IEvent, IEventActorContext<FuturesTdiSignalEventActor>, IStatusConsoleWriter, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FuturesTdiSignalGeneratedCompleteEvent).Name] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesTdiSignalGeneratedCompleteEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        },
        [typeof(FuturesRsiSignalsGeneratedEvent).Name] = async (evt, context, _, logger) =>
        {
            var e = (evt as FuturesRsiSignalsGeneratedEvent)!;
            return await e.ExecuteAsync(context, logger);
        }
    };

    protected override ValueTask OnStartup(IEventActorContext<FuturesTdiSignalEventActor> context)
    {
        _ = context;
        context.AddEventRouter(RsiSignalsRoute, Id);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask OnShutdown(IEventActorContext<FuturesTdiSignalEventActor> context)
    {
        context.RemoveEventRouter(RsiSignalsRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    protected override IEvent ParseMessage(IEventActorContext<FuturesTdiSignalEventActor> context, IActorMessage message)
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
        [FuturesTdiSignalGeneratedCompleteEvent.Verb] = msg => msg.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!,
        [FuturesRsiSignalsGeneratedEvent.Verb] = msg => msg.AsEvent<FuturesRsiSignalsGeneratedEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesTdiSignalEventActor> context, IEvent @event)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var eventName = @event.GetType().Name;
        if (!_receiveMap.TryGetValue(eventName, out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, dispatchContext, ActorContext.StatusConsoleWriter, ActorContext.Logger);
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesTdiSignalEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
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
            Context.Logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
