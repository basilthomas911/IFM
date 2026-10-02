using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the AcknowledgeIncident Supervisor complete event.</summary>
public static class AcknowledgeIncidentSupervisorActorComplete
{
    /// <summary>Records the AcknowledgeIncident complete outcome in Supervisor operation history.</summary>
    public static ValueTask ExecuteAsync(this AcknowledgeIncidentSupervisorActorCompleteEvent value,
        ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)
    {
        context.Incidents.Acknowledge(value.Target, value.Requester, value.Reason);
        return SupervisorTerminalProjection.Complete(context, value.CommandId, value.Target,
            value.ExpectedGeneration, SupervisorActorOperationKind.AcknowledgeIncident, value.Requester,
            value.Reason, value.TimeoutTicks, value.Stage);
    }
}
