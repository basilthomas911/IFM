using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.BrokerAccount.Event.Actor;

/// <summary>Routes immutable account observations through the BrokerAccount event mailbox.</summary>
public sealed class BrokerAccountEventActor(IEventActorContext<BrokerAccountEventActor> context)
    : BaseEventActor<BrokerAccountEventActor>(context, Typed(context).Logger)
{
    public const string ActorName = BrokerAccountActorNames.Event;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [BrokerAccountSnapshotObservedEvent.Verb] = message =>
                message.AsEvent<BrokerAccountSnapshotObservedEvent>()!
        }.ToFrozenDictionary(StringComparer.Ordinal);

    static readonly IReadOnlyDictionary<Type, Func<IBrokerAccountEventContext, IEvent, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IBrokerAccountEventContext, IEvent, ValueTask>>
        {
            [typeof(BrokerAccountSnapshotObservedEvent)] = static (eventContext, domainEvent) =>
                ((BrokerAccountSnapshotObservedEvent)domainEvent).ExecuteAsync(eventContext)
        }.ToFrozenDictionary();

    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<BrokerAccountEventActor> context,
        IActorMessage message) => ParseMappedEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(IEventActorContext<BrokerAccountEventActor> context,
        IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(Typed(context), @event);

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<BrokerAccountEventActor> context,
        ActorThreadId threadId, IEvent domainEvent, Exception exception) =>
        await exception.SendErrorEventAsync<global::TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(
            ErrorType.EventService, context).ConfigureAwait(false);

    static IBrokerAccountEventContext Typed(IEventActorContext<BrokerAccountEventActor> context) =>
        context as IBrokerAccountEventContext ??
        throw new ArgumentException("Typed BrokerAccount event context required.");
}

/// <summary>Exposes services used by BrokerAccount event handlers.</summary>
public interface IBrokerAccountEventContext : IEventActorContext<BrokerAccountEventActor>
{
    IActorService ActorService { get; }
    ILogger<BrokerAccountEventActor> Logger { get; }
}

/// <summary>Provides runtime services for the BrokerAccount event actor.</summary>
public sealed class BrokerAccountEventContext(IActorSupervisor supervisor, IActorService actorService,
    ILogger<BrokerAccountEventActor> logger)
    : EventActorContext(supervisor, new ActorMailboxId(ActorType.Event, BrokerAccountEventActor.ActorName)),
        IEventActorContext<BrokerAccountEventActor>, IBrokerAccountEventContext
{
    public IActorService ActorService { get; } = actorService;
    public ILogger<BrokerAccountEventActor> Logger { get; } = logger;
}
