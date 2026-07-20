using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for managing actors, their mailboxes, threads, and associated producers and consumers within an
/// actor-based system. Provides methods for adding, removing, and querying actors, thread states, and other related
/// components, as well as creating contexts for command, event, and query processing.
/// </summary>
/// <remarks>This interface is designed to facilitate the supervision and coordination of actors and their
/// associated resources in a distributed or concurrent environment. It provides mechanisms for managing the lifecycle
/// of actors and their threads, as well as for associating producers and consumers with actor mailboxes.
/// Implementations of this interface are expected to ensure thread safety and efficient resource management.</remarks>
public interface IActorSupervisor
{
    void AddActor(IActor actor);
    void RemoveActor(IActor actor);
    bool ActorExists(ActorMailboxId mailboxId);
    void AddThreadState(IActorState state);
    void RemoveThreadState(IActorState state);
    bool ThreadStateExists(ActorThreadId threadId);
    IActorState[] GetThreadStates(ActorMailboxId actorId);

    IActorMailbox CreateMailbox(ActorMailboxId mailboxId);

    ValueTask StartAsync(ActorMailboxId mailboxId); 
    ValueTask StopAsync(ActorMailboxId mailboxId);

    IActorThread GetThread(ActorThreadId threadId);
    ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct);

    void AddProducer(ActorMailboxId mailboxId, IActorProducer producer);
    void RemoveProducer(ActorMailboxId mailboxId);
    IActorProducer GetProducer(ActorMailboxId mailboxId);

    void AddJSProducer(ActorMailboxId mailboxId, IJSActorProducer producer);
    void RemoveJSProducer(ActorMailboxId mailboxId);
    IJSActorProducer GetJSProducer(ActorMailboxId mailboxId);

    void AddConsumer(ActorType actorType, IActorConsumer consumer);
    void AddConsumer(ActorType actorType, IJSActorConsumer consumer);
    void RemoveConsumer(ActorType actorType);

    void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    ValueTask RouteEventToAsync(NatsMsg<byte[]> routedFromMsg);

    ValueTask StartConsumersAsync();
    ValueTask StopConsumersAsync();

    ICommandActorContext CreateCommandActorContext(ActorMailboxId mailboxId);
    IEventActorContext CreateEventActorContext(ActorMailboxId mailboxId);
    IQueryActorContext CreateQueryActorContext(ActorMailboxId mailboxId);
    IDenormalizerActorContext CreateDenormalizerActorContext(ActorMailboxId mailboxId);

    IReadOnlyDictionary<ActorMailboxId, IActor> Children { get; }
    IReadOnlyDictionary<ActorThreadId, IActorState> ThreadState { get; }
    IContainerInstance Container { get; }

    /// * create as private to actor supervisor implementation only
    /// IImmutableDictionary<ActorMailboxId, IActorProducer> _producers { get; }
    /// IImmutableDictionary<ActorMailboxId, IActorProducer> _consumers { get; }
    /// IImmutableDictionary<ActorThreadId, IActorState> _threadStates { get; }
    IActorThreadPool ThreadPool { get; }

}

public interface IActorClient
{
    ValueTask TellAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId;
    ValueTask TellAsync(IEvent @event);
    ValueTask<TValue> AskAsync<TValue>(IQuery<TValue> query);
    ValueTask<ServiceResult<Guid>> AskAsync(ICommand<IActorEntityId> query);
}
