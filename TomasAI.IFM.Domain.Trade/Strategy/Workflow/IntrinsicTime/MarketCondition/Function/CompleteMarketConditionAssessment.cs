using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;

/// <summary>Builds completion candidates and records successfully committed assessments through the event map.</summary>
public static class CompleteMarketConditionAssessment
{
    /// <summary>Creates the typed completion, or observes the same event after the base commits it.</summary>
    public static FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> Complete(
        this FunctionEventContext<ExecuteMarketConditionAssessmentCommand> input, TimeProvider clock)
    {
        var c = input.Request ?? throw new ArgumentException("Completion requires its command.");
        if (input.Outcome is MarketConditionAssessmentCompletedEvent committed && input.Stage == FunctionFailureStage.Persistence)
        {
            MarketConditionTelemetry.RecordAssessment(MarketConditionAssessmentContracts.ReadResult(committed.Result),
                Math.Max(0, (clock.GetUtcNow().UtcDateTime - c.RequestedAtUtc).TotalMilliseconds));
            return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Complete(committed);
        }
        var outcome = input.Outcome as MarketConditionExecutionCompleted
            ?? throw new ArgumentException("Completion requires a calculated assessment.");
        var result = outcome.Result;
        var snapshot = outcome.Snapshot;
        var completed = new MarketConditionAssessmentCompletedEvent
        {
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, MarketConditionAssessmentCompletedEvent.Verb, c.EntityId.Format()),
            Id = result.ResultId, EntityId = c.WorkflowEntityId, CommandId = c.CommandId, AggregateId = c.EntityId.Format(),
            EventSource = $"{ExecuteMarketConditionAssessmentCommand.Actor}Actor", ReceivedOn = result.EvaluatedAtUtc,
            WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision, CorrelationId = c.CorrelationId, CausationId = c.CausationId,
            PipelineStage = StrategyWorkflowStage.MarketCondition, Result = StrategyStageResultEnvelope.CreateAssessment(result),
            CompletedAtUtc = clock.GetUtcNow().UtcDateTime, ExpiresAtUtc = c.ExpiresAtUtc, ParameterPayloadSha256 = c.ParameterPayloadSha256,
            MarketConditionSnapshotId = snapshot.SnapshotId, EvaluatedAtUtc = result.EvaluatedAtUtc, ValidUntilUtc = result.Assessment.ValidUntilUtc,
            RequestFingerprint = c.Fingerprint(), Snapshot = snapshot
        };
        return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Complete(completed);
    }
}
