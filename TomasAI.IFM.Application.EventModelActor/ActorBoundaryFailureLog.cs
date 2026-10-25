using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Compiled structured diagnostics for actor exception boundaries.</summary>
public static partial class ActorBoundaryFailureLog
{
    /// <summary>Records the exception raised while handling an earlier command failure.</summary>
    [LoggerMessage(7010, LogLevel.Error, "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}")]
    public static partial void HandlingFailed(ILogger logger, Exception exception, string actor,
        ActorThreadId threadId, string originalExceptionMessage);

    /// <summary>Records a terminal actor processing failure with its mailbox identity.</summary>
    [LoggerMessage(7011, LogLevel.Error, "{Component} failed for {ActorThreadId}.")]
    public static partial void Failed(ILogger logger, Exception exception, string component,
        ActorThreadId actorThreadId);
}
