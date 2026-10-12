using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;

/// <summary>Exposes only the operation read model and logger to Supervisor event handlers.</summary>
public interface ISupervisorEventActorContext : IEventActorContext<SupervisorEventActor>
{
    SupervisorOperationStore Operations { get; }
    ISupervisorIncidentStore Incidents { get; }
    ILogger<SupervisorEventActor> Logger { get; }
}
