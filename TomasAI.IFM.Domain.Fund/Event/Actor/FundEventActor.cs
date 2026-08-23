using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Event.Extensions;

namespace TomasAI.IFM.Domain.Fund.Event.Actor;

/// <summary>
/// Represents an event actor responsible for processing fund-related events within the actor system. Provides
/// mechanisms for parsing incoming messages, handling event execution, managing actor state, and reporting errors
/// specific to fund events.
/// </summary>
/// <param name="actorContext">The Fund event context resolved through the open-generic context registration.</param>
public class FundEventActor(
    IEventActorContext<FundEventActor> actorContext)
    : BaseEventActor<FundEventActor>(
        actorContext.Supervisor,
        actorContext.Logger,
        actorContext.ActorId)
{
    public const string Actor = "FundEvent";
    /// <summary>
    /// Gets the Fund-specific event context supplied when this actor is constructed.
    /// </summary>
    protected IFundEventContext FundEventContext { get; } = IsArgumentNull.Set(
        actorContext as IFundEventContext,
        nameof(actorContext))!;

    readonly ILogger<FundEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);
    readonly Dictionary<Type, Func<IEvent, IFundEventContext, ILogger, ValueTask<bool>>> _receiveMap = new()
    {
        [typeof(FundMaxProfitGeneratedEvent)] = async (evt, context, logger) =>
        {
            var e = (evt as FundMaxProfitGeneratedEvent)!;
            return await e.ExecuteAsync(context, logger);
        }
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
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
    /// <remarks>This dictionary enables efficient deserialization of incoming NATS messages by associating
    /// each event verb with a function that constructs the appropriate event type. The mapping assumes that each verb
    /// is unique and corresponds to a specific event class. The functions expect the message payload to be compatible
    /// with the target event type.</remarks>
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> _parseMap = new()
    {
        [FundMaxProfitGeneratedEvent.Verb] = msg => msg.AsEvent<FundMaxProfitGeneratedEvent>()!
    };

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="event"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        if (!_receiveMap.TryGetValue(@event.GetType(), out var receiveFunc))
            throw new InvalidOperationException($"Unable to resolve {Actor} event from message: {@event.Subject}");
        _ = await receiveFunc.Invoke(@event, FundEventContext, _logger).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles an exception that occurs during event actor processing and returns a failed service result containing
    /// error details.
    /// </summary>
    /// <remarks>This method sends an error event to the actor system to record the exception and returns a
    /// standardized failure result. The returned result can be used to propagate error details to callers or for
    /// logging purposes.</remarks>
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
            await ex.SendErrorEventAsync<IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
