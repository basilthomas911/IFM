using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Transaction.Event.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

/// <summary>
/// Represents an actor responsible for handling application-level events within the system. 
/// This actor processes incoming events, maps them to corresponding event handlers, 
/// and manages the actor's state in response to those events.
/// </summary>
/// <param name="actorContext">The typed Fund transaction event context.</param>
public class FundTransactionEventActor(IEventActorContext<FundTransactionEventActor> actorContext)
    : BaseEventActor<FundTransactionEventActor>(actorContext, actorContext.Logger)
{
    public const string Actor = "FundTransactionEvent";

    /// <summary>Gets the Fund transaction event context supplied to this actor.</summary>
    protected IFundTransactionEventContext FundTransactionEventContext { get; } =
        IsArgumentNull.Set(actorContext as IFundTransactionEventContext, nameof(actorContext))!;

    readonly ILogger<FundTransactionEventActor> _logger = IsArgumentNull.Set(actorContext.Logger);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FundTransactionEvent.Verb] = static message => ParseFundTransactionEvent<FundTransactionEvent>(message),
            [FundTransactionsEvent.Verb] = static message => ParseFundTransactionEvent<FundTransactionsEvent>(message),
            [EndOfDayFundTransactionProcessedEvent.Verb] = static message => ParseFundTransactionEvent<EndOfDayFundTransactionProcessedEvent>(message)
        };

    readonly IReadOnlyDictionary<Type, Func<IEvent, IFundTransactionEventContext, ValueTask>> _receiveMap = new Dictionary<Type, Func<IEvent, IFundTransactionEventContext, ValueTask>>()
    {
        [typeof(FundTransactionEvent)] = static (_, _) => ValueTask.CompletedTask,
        [typeof(FundTransactionsEvent)] = static (_, _) => ValueTask.CompletedTask,
        [typeof(EndOfDayFundTransactionProcessedEvent)] = static (_, _) => ValueTask.CompletedTask
    };

    /// <summary>
    /// Parses an incoming NATS message and resolves it to a corresponding event based on the message
    /// subject and verb.
    /// </summary>
    /// <param name="context">The actor context used for event processing. Cannot be null.</param>
    /// <param name="message">The NATS message containing the event data to parse. Cannot be null.</param>
    /// <returns>An event object representing the parsed event corresponding to the message and verb, or <see langword="null"/> if the message subject
    /// does not correspond to a known event (indicating the message should be ignored).</returns>
    protected override IEvent ParseMessage(IEventActorContext<FundTransactionEventActor> context, IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    static IEvent ParseFundTransactionEvent<TEvent>(IActorMessage message) where TEvent : class, IEvent
    {
        var @event = message.AsEvent<TEvent>()
            ?? throw new InvalidOperationException($"Unable to deserialize {typeof(TEvent).Name}.");
        @event.CheckForEmptyCommandId();
        return @event;
    }

    /// <summary>
    /// Receives an event and dispatches it to the appropriate handler based on the event's type. 
    /// If no handler is found for the event type, an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    /// <param name="context">The event actor context in which the event is being processed.</param>
    /// <param name="event">The event to be processed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException"></exception>
    protected override async ValueTask ReceiveAsync(IEventActorContext<FundTransactionEventActor> context, IEvent @event)
    {
        IsArgumentNull.Check(context);
        IsArgumentNull.Check(@event);
        var receiveFunc = ResolveMappedEventHandler(@event, _receiveMap);
        await receiveFunc(@event, FundTransactionEventContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles exceptions that occur during event processing by sending an error event to the event service.
    /// </summary>
    /// <param name="context">The event actor context in which the exception occurred.</param>
    /// <param name="threadId">The identifier of the actor thread where the exception was raised.</param>
    /// <param name="event">The event being processed when the exception was thrown.</param>
    /// <param name="ex">The exception that was thrown during actor processing.</param>
    /// <returns>A task that represents the asynchronous exception handling operation.</returns>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FundTransactionEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        try
        {
            IsArgumentNull.Check(context);
            await ex.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "Failed to send EventExceptionEvent for {Actor} actor.", Actor);
        }
    }
}
