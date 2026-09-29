using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Supervisor.Logging;

internal static partial class SupervisorPollingLog
{
    [LoggerMessage(
        EventId = 47001,
        Level = LogLevel.Information,
        Message = "Supervisor actor metrics heartbeat revision {Revision}: expected {ExpectedActors}, collected {CollectedActors}, failed {FailedActors}, healthy {HealthyActors}, degraded {DegradedActors}, critical {CriticalActors}, unknown {UnknownActors}, mailboxes {EntityMailboxes}, depth {QueueDepth}, rejected {Rejected}, failures {Failures}, elapsed {ElapsedMilliseconds} ms.")]
    internal static partial void Heartbeat(
        ILogger logger,
        long revision,
        int expectedActors,
        int collectedActors,
        int failedActors,
        int healthyActors,
        int degradedActors,
        int criticalActors,
        int unknownActors,
        int entityMailboxes,
        long queueDepth,
        long rejected,
        long failures,
        double elapsedMilliseconds);
}
