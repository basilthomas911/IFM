using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the Pause Supervisor complete event.</summary>
public static class PauseSupervisorActorComplete
{
    /// <summary>Records the Pause complete outcome in Supervisor operation history.</summary>
    public static ValueTask ExecuteAsync(this PauseSupervisorActorCompleteEvent value,
        ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)
        => SupervisorTerminalProjection.Complete(context, value.CommandId, value.Target,
            value.ExpectedGeneration, SupervisorActorOperationKind.Pause, value.Requester,
            value.Reason, value.TimeoutTicks, value.Stage);
}
