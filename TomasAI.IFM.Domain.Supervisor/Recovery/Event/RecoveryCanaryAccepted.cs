using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;

namespace TomasAI.IFM.Domain.Supervisor.Recovery.Event;

/// <summary>Projects one exact-generation recovery canary observation.</summary>
public static class RecoveryCanaryAccepted
{
    /// <summary>Records the canary without changing trading state.</summary>
    public static ValueTask ExecuteAsync(this RecoveryCanaryAcceptedEvent value,
        IRecoveryCanaryEventContext context, ILogger<RecoveryCanaryProjectorActor> logger)
    {
        context.Projection.Project(value);
        return ValueTask.CompletedTask;
    }
}
