using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Logging;

/// <summary>Contains compile-time generated logging declarations for Futures ITI realtime ingress.</summary>
internal static partial class FuturesItiSignalRealtimeLogging
{
    [LoggerMessage(EventId = 23805, Level = LogLevel.Information,
        Message = "Futures ITI Daily command generated SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate}")]
    public static partial void CommandGenerated(ILogger logger, Guid sourceEventId, Guid commandId,
        string contractId, DateOnly valueDate);

    [LoggerMessage(EventId = 23807, Level = LogLevel.Error,
        Message = "Futures ITI Daily command failed SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}")]
    public static partial void CommandFailed(ILogger logger, Guid sourceEventId, Guid commandId, string contractId,
        DateOnly valueDate, int errorCode, string errorMessage);

    [LoggerMessage(EventId = 23808, Level = LogLevel.Error,
        Message = "Futures ITI realtime ingress failed SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} Handler={Handler} Outcome=Failed ExceptionType={ExceptionType}")]
    public static partial void IngressFailed(ILogger logger, Exception exception, Guid sourceEventId, Guid commandId,
        string contractId, string handler, string exceptionType);

    [LoggerMessage(EventId = 23809, Level = LogLevel.Error,
        Message = "Futures ITI realtime ingress rejected SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} Handler={Handler} Outcome=Failed FailureReason={FailureReason}")]
    public static partial void IngressRejected(ILogger logger, Guid sourceEventId, Guid commandId,
        string contractId, string handler, string failureReason);
}
