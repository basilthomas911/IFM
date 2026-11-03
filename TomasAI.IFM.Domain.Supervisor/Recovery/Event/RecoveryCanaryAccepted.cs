using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event;

/// <summary>Projects one exact-generation recovery canary observation.</summary>
public static class RecoveryCanaryAccepted
{

    extension(RecoveryCanaryAcceptedEvent value)
    {
        /// <summary>Records the canary without changing trading state.</summary>
        public ValueTask ExecuteAsync(IRecoveryCanaryEventContext context, ILogger<RecoveryCanaryProjectorActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Projection.Project(value);
            return ValueTask.CompletedTask;
        }
    }
}
