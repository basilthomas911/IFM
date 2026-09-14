using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Logging;

/// <summary>Contains compile-time generated structured logging for the complete strategy workflow path.</summary>
internal static partial class IntrinsicTimeStrategyWorkflowLogging
{
    [LoggerMessage(EventId = 23900, Level = LogLevel.Trace,
        Message = "Strategy workflow command received CommandId={CommandId} EntityId={EntityId} Handler={Handler}")]
    public static partial void CommandReceived(ILogger logger, Guid commandId, string entityId, string handler);

    [LoggerMessage(EventId = 23901, Level = LogLevel.Debug,
        Message = "Strategy workflow command handled CommandId={CommandId} EntityId={EntityId} Handler={Handler} Success={Success} ErrorCode={ErrorCode} DurationMs={DurationMs}")]
    public static partial void CommandHandled(ILogger logger, Guid commandId, string entityId, string handler,
        bool success, int errorCode, double durationMs);

    [LoggerMessage(EventId = 23902, Level = LogLevel.Information,
        Message = "Strategy workflow state committed CommandId={CommandId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Status={Status} Outcome={Outcome}")]
    public static partial void StateCommitted(ILogger logger, Guid commandId, string workflowId, string entityId,
        long workflowRevision, string pipelineStage, string status, string outcome);

    [LoggerMessage(EventId = 23903, Level = LogLevel.Error,
        Message = "Strategy workflow command failed CommandId={CommandId} EntityId={EntityId} Handler={Handler} ExceptionType={ExceptionType} ErrorMessage={ErrorMessage}")]
    public static partial void CommandFailed(ILogger logger, Exception exception, Guid commandId, string entityId,
        string handler, string exceptionType, string errorMessage);

    [LoggerMessage(EventId = 23904, Level = LogLevel.Trace,
        Message = "Strategy workflow projection started SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Status={Status}")]
    public static partial void ProjectionStarted(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string status);

    [LoggerMessage(EventId = 23905, Level = LogLevel.Information,
        Message = "Strategy workflow state projected SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Status={Status} Outcome={Outcome}")]
    public static partial void ProjectionCompleted(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string status, string outcome);

    [LoggerMessage(EventId = 23906, Level = LogLevel.Trace,
        Message = "Committed strategy workflow state received SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Status={Status}")]
    public static partial void RealtimeStateReceived(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string status);

    [LoggerMessage(EventId = 23907, Level = LogLevel.Information,
        Message = "Strategy pipeline stage dispatched SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Handler={Handler}")]
    public static partial void StageDispatched(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string handler);

    [LoggerMessage(EventId = 23908, Level = LogLevel.Information,
        Message = "Strategy pipeline stage handler completed SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Handler={Handler} DurationMs={DurationMs}")]
    public static partial void StageHandlerCompleted(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string handler, double durationMs);

    [LoggerMessage(EventId = 23909, Level = LogLevel.Error,
        Message = "Strategy pipeline stage handler failed SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Handler={Handler} ExceptionType={ExceptionType} ErrorMessage={ErrorMessage}")]
    public static partial void StageHandlerFailed(ILogger logger, Exception exception, Guid sourceEventId,
        string workflowId, string entityId, long workflowRevision, string pipelineStage, string handler,
        string exceptionType, string errorMessage);

    [LoggerMessage(EventId = 23910, Level = LogLevel.Information,
        Message = "Strategy workflow terminal result SourceEventId={SourceEventId} WorkflowId={WorkflowId} EntityId={EntityId} WorkflowRevision={WorkflowRevision} PipelineStage={PipelineStage} Status={Status} Outcome={Outcome} ContinuationDecision={ContinuationDecision} ParameterSetId={ParameterSetId} ParameterSetVersion={ParameterSetVersion} DurationMs={DurationMs}")]
    public static partial void TerminalResult(ILogger logger, Guid sourceEventId, string workflowId,
        string entityId, long workflowRevision, string pipelineStage, string status, string outcome,
        string continuationDecision, Guid parameterSetId, int parameterSetVersion, double durationMs);
}
