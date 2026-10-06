using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.Telemetry;

/// <summary>Compiled operation boundary messages. Summaries must be bounded and constructed only after level/threshold checks.</summary>
public static partial class StructuredOperationLogging
{
    [LoggerMessage(7100, LogLevel.Information, "{Component}.{Method} completed; Arguments={Arguments}; Outcome={Outcome}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void Completed(ILogger logger, string component, string method, string arguments, string outcome, double elapsedMilliseconds);

    [LoggerMessage(7101, LogLevel.Error, "{Component}.{Method} failed; Arguments={Arguments}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void Failed(ILogger logger, string component, string method, string arguments, double elapsedMilliseconds, Exception exception);

    [LoggerMessage(7102, LogLevel.Warning, "{Component}.{Method} stalled; Subject={Subject}; Pending={Pending}; Limit={Limit}; Reason={Reason}; SuppressedCount={SuppressedCount}")]
    public static partial void Admission(ILogger logger, string component, string method, string subject, long pending, long limit, string reason, long suppressedCount);
    [LoggerMessage(7103, LogLevel.Information, "{Component}.{Method} result; CommandId={CommandId}; CommandName={CommandName}; Subject={Subject}; Outcome={Outcome}; ErrorCode={ErrorCode}; Stage={Stage}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void CommandResult(ILogger logger, string component, string method, Guid commandId, string commandName, TomasAI.IFM.Shared.EventModelActor.ActorSubject subject, string outcome, int errorCode, string stage, double elapsedMilliseconds);

    [LoggerMessage(7104, LogLevel.Warning, "{Component}.{Method} rejected; CommandId={CommandId}; CommandName={CommandName}; Subject={Subject}; ErrorCode={ErrorCode}; Reason={Reason}; Stage={Stage}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    public static partial void CommandRejected(ILogger logger, string component, string method, Guid commandId, string commandName, TomasAI.IFM.Shared.EventModelActor.ActorSubject subject, int errorCode, string reason, string stage, double elapsedMilliseconds);
}
