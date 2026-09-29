using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Logging;

/// <summary>Best-effort compiled exception logging for the critical polling path.</summary>
public sealed partial class SupervisorExceptionLog(ILogger<SupervisorExceptionLog> logger) : ISupervisorExceptionLog
{
    readonly ILogger<SupervisorExceptionLog> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public void ActorSnapshotFailed(ActorMailboxId actorId, Exception exception) =>
        Log.ActorSnapshotFailed(_logger, actorId.ActorType.ToString(), actorId.Name, exception);

    /// <inheritdoc />
    public void PollCycleFailed(Exception exception) => Log.PollCycleFailed(_logger, exception);

    /// <inheritdoc />
    public void HeartbeatLogFailed(Exception exception) => Log.HeartbeatLogFailed(_logger, exception);

    static partial class Log
    {
        [LoggerMessage(EventId = 47002, Level = LogLevel.Error,
            Message = "Supervisor failed to capture actor metrics for {ActorType}.{ActorName}.")]
        internal static partial void ActorSnapshotFailed(
            ILogger logger, string actorType, string actorName, Exception exception);

        [LoggerMessage(EventId = 47003, Level = LogLevel.Error,
            Message = "Supervisor actor metrics polling cycle failed.")]
        internal static partial void PollCycleFailed(ILogger logger, Exception exception);

        [LoggerMessage(EventId = 47004, Level = LogLevel.Error,
            Message = "Supervisor actor metrics heartbeat logging failed.")]
        internal static partial void HeartbeatLogFailed(ILogger logger, Exception exception);
    }
}
