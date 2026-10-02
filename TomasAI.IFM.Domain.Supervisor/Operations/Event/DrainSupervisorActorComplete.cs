using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the Drain Supervisor complete event.</summary>
public static class DrainSupervisorActorComplete
{
    /// <summary>Records the Drain complete outcome in Supervisor operation history.</summary>
    public static ValueTask ExecuteAsync(this DrainSupervisorActorCompleteEvent value,
        ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)
        => SupervisorTerminalProjection.Complete(context, value.CommandId, value.Target,
            value.ExpectedGeneration, SupervisorActorOperationKind.Drain, value.Requester,
            value.Reason, value.TimeoutTicks, value.Stage);
}
