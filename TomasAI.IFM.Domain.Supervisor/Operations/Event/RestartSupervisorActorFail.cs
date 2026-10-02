using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the Restart Supervisor fail event.</summary>
public static class RestartSupervisorActorFail
{
    /// <summary>Records the Restart fail outcome in Supervisor operation history.</summary>
    public static ValueTask ExecuteAsync(this RestartSupervisorActorFailEvent value,
        ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)
        => SupervisorTerminalProjection.Fail(context, logger, value.CommandId, value.Target,
            value.ExpectedGeneration, SupervisorActorOperationKind.Restart, value.Requester,
            value.Reason, value.TimeoutTicks, value.Outcome, value.Stage, value.ErrorMessage);
}
