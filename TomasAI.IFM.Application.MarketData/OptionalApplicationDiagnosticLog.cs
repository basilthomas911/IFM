namespace TomasAI.IFM.Application.MarketData;

/// <summary>Cached structured log delegates for application lifecycle diagnostics.</summary>
internal static partial class OptionalApplicationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2101, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Symbol discovery {Dataset}: excluded {Invalid} incomplete definitions and {Unresolved} options without a unique current outright-futures underlying.")]
    internal static partial void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Dataset, int Invalid, int Unresolved);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2102, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Composition discovery reference qualification: contracts={Count}, elapsedMs={ElapsedMs}")]
    internal static partial void Write2(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, int Count, long ElapsedMs);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2103, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Composition discovery reference qualification rejected: code={Code}, contract={ContractId}, detail={Detail}, elapsedMs={ElapsedMs}")]
    internal static partial void Write3(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Code, string ContractId, string Detail, long ElapsedMs);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2104, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Composition discovery context preparation: contracts={Count}, elapsedMs={ElapsedMs}")]
    internal static partial void Write4(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, int Count, long ElapsedMs);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2105, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Composition discovery first worker acquire: contracts={Count}, elapsedMs={ElapsedMs}, failure={Failure}")]
    internal static partial void Write5(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, int Count, long ElapsedMs, string Failure);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2106, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Composition discovery worker acquire rejected: code={Code}, detail={Detail}, elapsedMs={ElapsedMs}")]
    internal static partial void Write6(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Code, string Detail, long ElapsedMs);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2107, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Monitoring lease renewal rejected; MethodName={MethodName} ScopeId={ScopeId} LeaseId={LeaseId} GenerationId={GenerationId}")]
    internal static partial void Write7(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string ScopeId, global::System.Guid LeaseId, global::System.Guid GenerationId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2108, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Monitoring lease renewal failed; MethodName={MethodName} ScopeId={ScopeId} LeaseId={LeaseId} GenerationId={GenerationId}")]
    internal static partial void Write8(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string MethodName, string ScopeId, global::System.Guid LeaseId, global::System.Guid GenerationId);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2109, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Composition context refresh rejected for scope {Scope}: {Reason}")]
    internal static partial void Write9(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Scope, string Reason);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2110, Level = global::Microsoft.Extensions.Logging.LogLevel.Warning, Message = "Composition context refresh failed for scope {Scope}: {ErrorType}")]
    internal static partial void Write10(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Scope, string ErrorType);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2111, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Stored {Count} exact definitions for {Dataset}")]
    internal static partial void Write11(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, long Count, string Dataset);

    [global::Microsoft.Extensions.Logging.LoggerMessage(EventId = 2112, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Published instrument definitions {Snapshot}: {Records} records and {Products} unique products")]
    internal static partial void Write12(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.Guid Snapshot, long Records, int Products);
}
