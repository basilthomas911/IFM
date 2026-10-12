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
