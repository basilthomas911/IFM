namespace TomasAI.IFM.Domain.Reference;

/// <summary>Cached structured log delegates for actor lifecycle diagnostics.</summary>
internal static class ActorLifecycleDiagnosticLog
{
    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, object?, string, global::System.Exception?> Write1Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, object?, string>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}");
    internal static void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, object? value1, string value2) => Write1Delegate(logger, value0, value1, value2, exception);
}
