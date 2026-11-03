using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the Stop Supervisor complete event.</summary>
public static class StopSupervisorActorComplete
{

    extension(StopSupervisorActorCompleteEvent value)
    {
        /// <summary>Records the Stop complete outcome in Supervisor operation history.</summary>
        public ValueTask ExecuteAsync(ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(value);
            return SupervisorTerminalProjection.Complete(context, value.CommandId, value.Target,
                value.ExpectedGeneration, SupervisorActorOperationKind.Stop, value.Requester,
                value.Reason, value.TimeoutTicks, value.Stage);
        }
    }
}
