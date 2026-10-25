namespace TomasAI.IFM.Application.CommandPipeline;

/// <summary>Cached structured log delegates for actor lifecycle diagnostics.</summary>
internal static class PersistenceDiagnosticLog
{
    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, global::System.Exception?> Write1Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Execution of command \u0027{CommandName}\u0027 failed");
    internal static void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0) => Write1Delegate(logger, value0, exception);
}
