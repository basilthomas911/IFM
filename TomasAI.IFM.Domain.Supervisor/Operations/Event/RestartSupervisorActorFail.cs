using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Operations.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Operations.Events;

namespace TomasAI.IFM.Domain.Supervisor.Operations.Event;

/// <summary>Projects the Restart Supervisor fail event.</summary>
public static class RestartSupervisorActorFail
{

    extension(RestartSupervisorActorFailEvent value)
    {
        /// <summary>Records the Restart fail outcome in Supervisor operation history.</summary>
        public ValueTask ExecuteAsync(ISupervisorEventActorContext context, ILogger<SupervisorEventActor> logger)

        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(value);
            return SupervisorTerminalProjection.Fail(context, logger, value.CommandId, value.Target,
                value.ExpectedGeneration, SupervisorActorOperationKind.Restart, value.Requester,
                value.Reason, value.TimeoutTicks, value.Outcome, value.Stage, value.ErrorMessage);
        }
    }
}
