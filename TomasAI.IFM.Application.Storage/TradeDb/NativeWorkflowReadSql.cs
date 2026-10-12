namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Native PostgreSQL workflow detail and ordered history queries.</summary>
internal static class NativeWorkflowReadSql
{
    internal const string GetIntrinsicTimeStrategyWorkflow = """
SELECT workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
       contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
       status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
       statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow
WHERE workflowId = $1;
""";

    internal const string GetActiveIntrinsicTimeStrategyWorkflow = """
SELECT workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
       currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
       startedAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = $1;
""";

    internal const string GetIntrinsicTimeStrategyWorkflowStartAttempts = """
SELECT workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
       startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId
FROM trade.intrinsic_time_strategy_workflow_start_attempt
WHERE workflowEntityId = $1 AND requestedAtUtc < $2
ORDER BY requestedAtUtc DESC, requestedWorkflowId DESC
LIMIT $3;
""";

    internal const string GetIntrinsicTimeStrategyWorkflowTimeline = """
SELECT workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
       eventSchemaVersion, eventPayload, occurredAtUtc
FROM trade.intrinsic_time_strategy_workflow_timeline
WHERE workflowId = $1 AND eventId > $2
ORDER BY eventId ASC
LIMIT $3;
""";

    internal const string GetIntrinsicTimeStrategyWorkflowsByEntity = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_entity
WHERE workflowEntityId = $1 AND startedAtUtc < $2
ORDER BY startedAtUtc DESC, workflowId DESC
LIMIT $3;
""";

    internal const string GetIntrinsicTimeStrategyWorkflowsByStatusDay = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_status_day
WHERE status = $1 AND startedDate = $2
ORDER BY startedAtUtc DESC, workflowId DESC
LIMIT $3;
""";

}
