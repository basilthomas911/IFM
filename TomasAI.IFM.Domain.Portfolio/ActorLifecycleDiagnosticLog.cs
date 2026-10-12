namespace TomasAI.IFM.Domain.Portfolio;

/// <summary>Cached structured log delegates for actor lifecycle diagnostics.</summary>
internal static class ActorLifecycleDiagnosticLog
{
    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, string, object?, global::System.Exception?> Write1Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, string, object?>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "{Component}.{Method} Financial history event {EventId} remains pending.");
    internal static void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, string value1, object? value2) => Write1Delegate(logger, value0, value1, value2, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, string, global::System.Exception?> Write2Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, string>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "{Component}.{Method} Financial history projection is pending; PostgreSQL receipts remain authoritative.");
    internal static void Write2(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, string value1) => Write2Delegate(logger, value0, value1, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, string, object?, global::System.Exception?> Write3Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, string, object?>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "{Component}.{Method} Financial operation {OperationId} committed; durable history notification is pending.");
    internal static void Write3(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, string value1, object? value2) => Write3Delegate(logger, value0, value1, value2, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, string, global::System.Exception?> Write4Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, string>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "{Component}.{Method} Capacity expiry dispatcher is pending; financial obligations are retained.");
    internal static void Write4(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, string value1) => Write4Delegate(logger, value0, value1, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, string, object?, global::System.Exception?> Write5Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, string, object?>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "{Component}.{Method} Capacity expiry operation {OperationId} remains pending; no release is inferred.");
    internal static void Write5(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, string value1, object? value2) => Write5Delegate(logger, value0, value1, value2, exception);
}
