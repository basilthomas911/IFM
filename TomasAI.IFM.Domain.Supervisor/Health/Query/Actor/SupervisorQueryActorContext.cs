using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;

/// <summary>Exposes only metrics and logging to the Supervisor health query.</summary>
public interface ISupervisorQueryActorContext : IQueryActorContext<SupervisorQueryActor>
{
    ISupervisorActorMetricsState ActorMetrics { get; }
    ILogger<SupervisorQueryActor> Logger { get; }
}

/// <summary>Provides the closed-generic Supervisor query context.</summary>
public sealed class SupervisorQueryActorContext(
    IActorSupervisor actorSupervisor,
    ISupervisorActorMetricsState actorMetrics,
    ILogger<SupervisorQueryActor> logger)
    : QueryActorContext(actorSupervisor, new(ActorType.Query, SupervisorQueryActor.ActorName)),
        IQueryActorContext<SupervisorQueryActor>, ISupervisorQueryActorContext
{
    public ISupervisorActorMetricsState ActorMetrics { get; } = actorMetrics;
    public ILogger<SupervisorQueryActor> Logger { get; } = logger;
}
