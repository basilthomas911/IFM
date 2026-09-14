using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Logging;

/// <summary>Contains compile-time generated logging declarations for Futures ITI projection and completion.</summary>
internal static partial class FuturesItiSignalEventLogging
{
    [LoggerMessage(EventId = 23820, Level = LogLevel.Trace,
        Message = "Futures ITI projection started SourceEventId={SourceEventId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}")]
    public static partial void ProjectionStarted(ILogger logger, Guid sourceEventId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod);

    [LoggerMessage(EventId = 23821, Level = LogLevel.Information,
        Message = "Futures ITI projection applied SourceEventId={SourceEventId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} SequenceId={SequenceId} Outcome=ProjectionCompleted")]
    public static partial void ProjectionCompleted(ILogger logger, Guid sourceEventId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod, long sequenceId);

    [LoggerMessage(EventId = 23822, Level = LogLevel.Trace,
        Message = "Futures ITI completion received SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}")]
    public static partial void CompletionReceived(ILogger logger, Guid sourceEventId, Guid commandId,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod);

    [LoggerMessage(EventId = 23823, Level = LogLevel.Information,
        Message = "Futures ITI completion handled SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} WorkflowRequested={WorkflowRequested} DerivedPeriodsSucceeded={DerivedPeriodsSucceeded} DurationMs={DurationMs} Outcome=CompletionHandled")]
    public static partial void CompletionHandled(ILogger logger, Guid sourceEventId, Guid commandId,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, bool workflowRequested,
        bool derivedPeriodsSucceeded, double durationMs);

    [LoggerMessage(EventId = 23824, Level = LogLevel.Trace,
        Message = "Derived Futures ITI command requested ParentEventId={ParentEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}")]
    public static partial void DerivedCommandRequested(ILogger logger, Guid parentEventId, Guid commandId,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod);

    [LoggerMessage(EventId = 23825, Level = LogLevel.Debug,
        Message = "Derived Futures ITI command accepted ParentEventId={ParentEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}")]
    public static partial void DerivedCommandAccepted(ILogger logger, Guid parentEventId, Guid commandId,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod);

    [LoggerMessage(EventId = 23826, Level = LogLevel.Warning,
        Message = "Derived Futures ITI command rejected ParentEventId={ParentEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}")]
    public static partial void DerivedCommandRejected(ILogger logger, Guid parentEventId, Guid commandId,
        string contractId, DateOnly valueDate, TimeFrameType timePeriod, int errorCode, string errorMessage);

    [LoggerMessage(EventId = 23827, Level = LogLevel.Information,
        Message = "Timeframe strategy workflow requested SourceEventId={SourceEventId} CommandId={CommandId} WorkflowId={WorkflowId} EntityId={EntityId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}")]
    public static partial void WorkflowRequested(ILogger logger, Guid sourceEventId, Guid commandId, string workflowId,
        string entityId, string contractId, DateOnly valueDate, TimeFrameType timePeriod);

    [LoggerMessage(EventId = 23828, Level = LogLevel.Error,
        Message = "Futures ITI completion failed SourceEventId={SourceEventId} CommandId={CommandId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} Handler={Handler} Outcome=Failed ExceptionType={ExceptionType}")]
    public static partial void CompletionFailed(ILogger logger, Exception exception, Guid sourceEventId,
        Guid commandId, string contractId, DateOnly valueDate, TimeFrameType timePeriod,
        string handler, string exceptionType);
}
