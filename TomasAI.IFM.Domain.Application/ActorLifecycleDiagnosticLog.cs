namespace TomasAI.IFM.Domain.Application;

/// <summary>Cached structured log delegates for actor lifecycle diagnostics.</summary>
internal static class ActorLifecycleDiagnosticLog
{
    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, object?, object?, object?, object?, object?, global::System.Exception?> Write1Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, object?, object?, object?, object?, object?>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "{ServiceId} failure event {EventId} for command {CommandId}, entity {EntityId}, code {ErrorCode}: {ErrorMessage}");
    internal static void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, object? value1, object? value2, object? value3, object? value4, object? value5) => Write1Delegate(logger, value0, value1, value2, value3, value4, value5, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, object?, object?, object?, object?, global::System.Exception?> Write2Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<object?, object?, object?, object?>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Application startup activity {Activity} failed. ValueDate={ValueDate}; CommandId={CommandId}; CorrelationId={CorrelationId}");
    internal static void Write2(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, object? value0, object? value1, object? value2, object? value3) => Write2Delegate(logger, value0, value1, value2, value3, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.TimeSpan, global::System.Exception?> Write3Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<global::System.TimeSpan>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "Timed out publishing Application lifecycle status to the System Console after {Timeout}.");
    internal static void Write3(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.TimeSpan value0) => Write3Delegate(logger, value0, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.Exception?> Write4Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Unable to publish Application lifecycle status to the System Console.");
    internal static void Write4(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception) => Write4Delegate(logger, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.TimeSpan, global::System.Exception?> Write5Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<global::System.TimeSpan>(global::Microsoft.Extensions.Logging.LogLevel.Warning, new global::Microsoft.Extensions.Logging.EventId(0), "Timed out publishing Application lifecycle failure to the System Console after {Timeout}.");
    internal static void Write5(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.TimeSpan value0) => Write5Delegate(logger, value0, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.Exception?> Write6Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Unable to publish Application lifecycle failure to the System Console.");
    internal static void Write6(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception) => Write6Delegate(logger, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.Exception?> Write7Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "A timed-out Application lifecycle System Console write later failed.");
    internal static void Write7(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception) => Write7Delegate(logger, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, object?, object?, object?, object?, object?, global::System.Exception?> Write8Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, object?, object?, object?, object?, object?>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "{ServiceId} failure event {EventId} for command {CommandId}, entity {EntityId}, code {ErrorCode}: {ErrorMessage}");
    internal static void Write8(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, object? value1, object? value2, object? value3, object? value4, object? value5) => Write8Delegate(logger, value0, value1, value2, value3, value4, value5, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, object?, global::System.Exception?> Write9Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<object?>(global::Microsoft.Extensions.Logging.LogLevel.Information, new global::Microsoft.Extensions.Logging.EventId(0), "Application shutdown command {CommandId} was observed; production shutdown orchestration remains deferred.");
    internal static void Write9(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, object? value0) => Write9Delegate(logger, value0, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, global::System.Exception?> Write10Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Unable to publish deferred shutdown status to the System Console.");
    internal static void Write10(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception) => Write10Delegate(logger, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, global::System.Exception?> Write11Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Failed to send EventExceptionEvent for {Actor} actor.");
    internal static void Write11(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0) => Write11Delegate(logger, value0, exception);

    static readonly global::System.Action<global::Microsoft.Extensions.Logging.ILogger, string, object?, string, global::System.Exception?> Write12Delegate = global::Microsoft.Extensions.Logging.LoggerMessage.Define<string, object?, string>(global::Microsoft.Extensions.Logging.LogLevel.Error, new global::Microsoft.Extensions.Logging.EventId(0), "Error handling exception for {Actor} command in thread {ThreadId}: {OriginalExceptionMessage}");
    internal static void Write12(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string value0, object? value1, string value2) => Write12Delegate(logger, value0, value1, value2, exception);
}
