using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;

/// <summary>Routes normalized broker observations through the BrokerOrder event mailbox.</summary>
public sealed class BrokerOrderEventActor(IEventActorContext<BrokerOrderEventActor> context)
    : BaseEventActor<BrokerOrderEventActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerOrderActorNames.Event;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [BrokerOrderObservationReceivedEvent.Verb] = message =>
                message.AsEvent<BrokerOrderObservationReceivedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<IBrokerOrderEventContext, IEvent, ILogger<BrokerOrderEventActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IBrokerOrderEventContext, IEvent, ILogger<BrokerOrderEventActor>, ValueTask>>
        {
            [typeof(BrokerOrderObservationReceivedEvent)] = static (eventContext, domainEvent, logger) =>
                ((BrokerOrderObservationReceivedEvent)domainEvent).ExecuteAsync(eventContext, logger)
        }.ToFrozenDictionary();

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<BrokerOrderEventActor> context,
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<BrokerOrderEventActor> context,
        IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(Typed(context), @event, Typed(context).Logger);

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<BrokerOrderEventActor> context,
        ActorThreadId threadId, IEvent domainEvent, Exception exception) =>
        await exception.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            ErrorType.EventService, context).ConfigureAwait(false);

    static IBrokerOrderEventContext Typed(IEventActorContext<BrokerOrderEventActor> context) =>
        context as IBrokerOrderEventContext ??
        throw new ArgumentException("Typed BrokerOrder event context required.");
}

/// <summary>Exposes services used by BrokerOrder event handlers.</summary>
public interface IBrokerOrderEventContext : IEventActorContext<BrokerOrderEventActor>
{
    IActorService ActorService { get; }
    ILogger<BrokerOrderEventActor> Logger { get; }
}

/// <summary>Provides runtime services for the BrokerOrder event actor.</summary>
public sealed class BrokerOrderEventContext(IActorSupervisor supervisor, IActorService actorService,
    ILogger<BrokerOrderEventActor> logger)
    : EventActorContext(supervisor, new ActorMailboxId(ActorType.Event, BrokerOrderEventActor.ActorName)),
        IEventActorContext<BrokerOrderEventActor>, IBrokerOrderEventContext
{
    public IActorService ActorService { get; } = actorService;
    public ILogger<BrokerOrderEventActor> Logger { get; } = logger;
}
