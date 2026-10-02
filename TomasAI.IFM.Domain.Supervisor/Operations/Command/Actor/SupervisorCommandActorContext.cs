using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Supervisor.Operations.Command.State;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Command.Actor;

/// <summary>Exposes only lifecycle, incident, authorization, and logging capabilities to Supervisor commands.</summary>
public interface ISupervisorCommandActorContext : ICommandActorContext<SupervisorCommandActor>
{
    ISupervisorManagedActorLifecycle ManagedActors { get; }
    ISupervisorIncidentReadStore Incidents { get; }
    ISupervisorOperatorAuthorizer Authorizer { get; }
    IEventSourceActorStateRepository<SupervisorCommandState> StateRepository { get; }
    IEventProjector<SupervisorCommandActor> EventProjector { get; }
    ILogger<SupervisorCommandActor> Logger { get; }
}

/// <summary>Provides the closed-generic, privileged Supervisor command context.</summary>
public sealed class SupervisorCommandActorContext(IActorSupervisor actorSupervisor,
    ISupervisorManagedActorLifecycle managedActors, ISupervisorIncidentStore incidents,
    ISupervisorOperatorAuthorizer authorizer, ILogger<SupervisorCommandActor> logger)
    : CommandActorContext(actorSupervisor, new(ActorType.Command, SupervisorCommandActor.ActorName)),
        ICommandActorContext<SupervisorCommandActor>, ISupervisorCommandActorContext
{
    readonly Lazy<IEventSourceActorStateRepository<SupervisorCommandState>> _repository =
        new(() => actorSupervisor.Container.Resolve<IEventSourceActorStateRepository<SupervisorCommandState>>());
    readonly Lazy<IEventProjector<SupervisorCommandActor>> _projector =
        new(() => actorSupervisor.Container.Resolve<IEventProjector<SupervisorCommandActor>>());
    public ISupervisorManagedActorLifecycle ManagedActors { get; } = managedActors;
    public ISupervisorIncidentReadStore Incidents { get; } = incidents;
    public ISupervisorOperatorAuthorizer Authorizer { get; } = authorizer;
    public IEventSourceActorStateRepository<SupervisorCommandState> StateRepository => _repository.Value;
    public IEventProjector<SupervisorCommandActor> EventProjector => _projector.Value;
    public ILogger<SupervisorCommandActor> Logger { get; } = logger;
}
