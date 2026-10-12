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
