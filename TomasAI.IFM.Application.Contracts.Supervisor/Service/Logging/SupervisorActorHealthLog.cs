using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Logging;

/// <summary>Allocation-efficient structured logging for actor-thread health transitions.</summary>
public static class SupervisorActorHealthLog
{
    static readonly Action<ILogger, ActorThreadId, int, int, double, SupervisorActorHealth, Exception?> AtLimitMessage =
        LoggerMessage.Define<ActorThreadId, int, int, double, SupervisorActorHealth>(
            LogLevel.Warning,
            new EventId(7101, nameof(ActorThreadAtLimit)),
            "Actor thread {ActorThreadId} remains at mailbox limit {QueueDepth}/{QueueCapacity} for {LimitDurationSeconds} seconds; health is {Health}.");

    /// <summary>Logs one policy-rate-limited warning for an actor thread at its mailbox limit.</summary>
    public static void ActorThreadAtLimit(
        ILogger logger,
        ActorThreadId threadId,
        int queueDepth,
        int queueCapacity,
        TimeSpan limitDuration,
        SupervisorActorHealth health) =>
        AtLimitMessage(logger, threadId, queueDepth, queueCapacity, limitDuration.TotalSeconds, health, null);
}
