using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Compiled structured log messages shared by every actor execution path.</summary>
static class ActorMessageProcessingLog
{
    static readonly Action<ILogger, ActorThreadId, string, Exception?> EntryMessage =
        LoggerMessage.Define<ActorThreadId, string>(LogLevel.Information, new(7001, nameof(Entry)),
            "Actor message entry: {ActorThreadId}; verb {Verb}.");
    static readonly Action<ILogger, ActorThreadId, string, string, double, Exception?> ExitMessage =
        LoggerMessage.Define<ActorThreadId, string, string, double>(LogLevel.Information, new(7002, nameof(Exit)),
            "Actor message exit: {ActorThreadId}; verb {Verb}; outcome {Outcome}; elapsed {ElapsedMilliseconds} ms.");
    static readonly Action<ILogger, ActorThreadId, string, double, Exception?> ExceptionMessage =
        LoggerMessage.Define<ActorThreadId, string, double>(LogLevel.Error, new(7003, nameof(Failed)),
            "Actor message exception: {ActorThreadId}; verb {Verb}; elapsed {ElapsedMilliseconds} ms.");

    internal static void Entry(ILogger logger, ActorThreadId id, string verb) => EntryMessage(logger, id, verb, null);
    internal static void Exit(ILogger logger, ActorThreadId id, string verb, string outcome, long started) =>
        ExitMessage(logger, id, verb, outcome, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, null);
    internal static void Failed(ILogger logger, ActorThreadId id, string verb, long started, Exception exception) =>
        ExceptionMessage(logger, id, verb, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception);
}
