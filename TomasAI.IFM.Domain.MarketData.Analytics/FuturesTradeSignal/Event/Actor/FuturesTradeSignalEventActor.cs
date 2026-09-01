using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event.Actor;

/// <summary>Provides the FuturesTradeSignalEventActor implementation.</summary>
public class FuturesTradeSignalEventActor(
    IEventActorContext<FuturesTradeSignalEventActor> actorContext)
    : BaseEventActor<FuturesTradeSignalEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesTradeSignalEventContext ActorContext =>
        IsArgumentNull.Set(Context as IFuturesTradeSignalEventContext, nameof(Context))!;

    public const string Actor = "FuturesTradeSignalEvent";

    readonly IReadOnlyDictionary<Type, Func<IEvent, IEventActorContext<FuturesTradeSignalEventActor>, IStatusConsoleWriter, ILogger, ValueTask<bool>>> _receiveMap = new Dictionary<Type, Func<IEvent, IEventActorContext<FuturesTradeSignalEventActor>, IStatusConsoleWriter, ILogger, ValueTask<bool>>>()
    {
        [typeof(FuturesTradeSignalUpdatedCompleteEvent)] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesTradeSignalUpdatedCompleteEvent)!;
            await context.PublishMarketOutlookComponentAsync(e).ConfigureAwait(false);
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        },
        [typeof(FuturesItiSignalHoldTradeChangedEvent)] = async (evt, context, statusConsoleWriter, logger) =>
        {
            var e = (evt as FuturesItiSignalHoldTradeChangedEvent)!;
            return await e.ExecuteAsync(context, statusConsoleWriter, logger);
        }
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb.</returns>
    protected override IEvent ParseMessage(IEventActorContext<FuturesTradeSignalEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <summary>
    /// Maps event verb strings to factory functions that convert NATS messages into corresponding event instances.
    /// </summary>
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>()
    {
        [FuturesTradeSignalUpdatedCompleteEvent.Verb] = msg => msg.AsEvent<FuturesTradeSignalUpdatedCompleteEvent>()!,
        [FuturesItiSignalHoldTradeChangedEvent.Verb] = msg => msg.AsEvent<FuturesItiSignalHoldTradeChangedEvent>()!
    };

    /// <summary>
    /// Asynchronously processes an event received by the event actor using the appropriate event handler.
    /// </summary>
    /// <param name="context">The context in which the event actor is executing. Cannot be null.</param>
    /// <param name="event">The event to be processed by the event actor. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous receive operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the event type.</exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesTradeSignalEventActor> context, IEvent @event)
    {
        var dispatchContext = context;
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var receiveFunc = ResolveMappedEventHandler(@event, _receiveMap);
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
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesTradeSignalEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
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
