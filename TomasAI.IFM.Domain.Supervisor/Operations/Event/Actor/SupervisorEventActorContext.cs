using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;

/// <summary>Provides the closed-generic Supervisor event actor context.</summary>
public sealed class SupervisorEventActorContext(IActorSupervisor supervisor,
    SupervisorOperationStore operations, ISupervisorIncidentStore incidents,
    ILogger<SupervisorEventActor> logger)
    : EventActorContext(supervisor, new(ActorType.Event, SupervisorEventActor.ActorName)),
ISupervisorEventActorContext
{
    public SupervisorOperationStore Operations { get; } = operations;
    public ISupervisorIncidentStore Incidents { get; } = incidents;
    public ILogger<SupervisorEventActor> Logger { get; } = logger;
}
