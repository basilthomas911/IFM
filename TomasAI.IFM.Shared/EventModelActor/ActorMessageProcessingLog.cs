using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Compiled actor diagnostics; routine realtime messages remain controlled by the suppression policy.</summary>
static partial class ActorMessageProcessingLog
{
    [LoggerMessage(7001, LogLevel.Information, "{Component}.{Method} entry; ActorThreadId={ActorThreadId}; Verb={Verb}; Subject={Subject}")]
    internal static partial void Entry(ILogger logger, ActorThreadId actorThreadId, string verb, ActorSubject subject, string method, string component = "ActorRuntime");

    [LoggerMessage(7002, LogLevel.Information, "{Component}.{Method} exit; ActorThreadId={ActorThreadId}; Verb={Verb}; Subject={Subject}; Outcome={Outcome}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    static partial void ExitMessage(ILogger logger, ActorThreadId actorThreadId, string verb, ActorSubject subject, string method, string outcome, double elapsedMilliseconds, string component = "ActorRuntime");

    [LoggerMessage(7003, LogLevel.Error, "{Component}.{Method} exception; ActorThreadId={ActorThreadId}; Verb={Verb}; Subject={Subject}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    static partial void ExceptionMessage(ILogger logger, ActorThreadId actorThreadId, string verb, ActorSubject subject, string method, double elapsedMilliseconds, Exception exception, string component = "ActorRuntime");

    internal static void Exit(ILogger logger, ActorThreadId id, string verb, string outcome, long started, ActorSubject subject, string method) =>
        ExitMessage(logger, id, verb, subject, method, outcome, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    internal static void Failed(ILogger logger, ActorThreadId id, string verb, long started, Exception exception, ActorSubject subject, string method) =>
        ExceptionMessage(logger, id, verb, subject, method, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception);
}
