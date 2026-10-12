using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;

/// <summary>Provides the closed-generic Supervisor query context.</summary>
public sealed class SupervisorQueryActorContext(
    IActorSupervisor actorSupervisor,
    ISupervisorActorMetricsState actorMetrics,
    ILogger<SupervisorQueryActor> logger)
    : QueryActorContext(actorSupervisor, new(ActorType.Query, SupervisorQueryActor.ActorName)),
ISupervisorQueryActorContext
{
    public ISupervisorActorMetricsState ActorMetrics { get; } = actorMetrics;
    public ILogger<SupervisorQueryActor> Logger { get; } = logger;
}
