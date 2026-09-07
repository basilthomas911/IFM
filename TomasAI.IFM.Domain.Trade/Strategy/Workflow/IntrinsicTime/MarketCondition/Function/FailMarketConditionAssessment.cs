using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;

/// <summary>Maps domain, transport and lifecycle failures to a non-durable assessment response.</summary>
public static class FailMarketConditionAssessment
{
    /// <summary>Classifies one failure and constructs its terminal event without projection or persistence.</summary>
    public static FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent> Fail(
        this FunctionEventContext<ExecuteMarketConditionAssessmentCommand> input, TimeProvider clock)
    {
        var c = input.Request;
        var outcome = input.Outcome as MarketConditionExecutionFailed;
        if (!input.IsConflict && outcome is null && input.Exception is null)
            throw new ArgumentException("Failure requires an outcome, conflict or exception.");
        var category = input.IsConflict ? MarketConditionFailureCategory.ContractInvalid : outcome?.Category ??
            (input.Exception is TimeoutException ? MarketConditionFailureCategory.Timeout : input.Stage switch
            {
                FunctionFailureStage.Loading or FunctionFailureStage.Persistence => MarketConditionFailureCategory.PersistenceFailed,
                FunctionFailureStage.Projection => MarketConditionFailureCategory.ProjectionFailed,
                FunctionFailureStage.Execution => MarketConditionFailureCategory.CalculationFailed,
                _ => MarketConditionFailureCategory.ContractInvalid
            });
        var reason = input.IsConflict ? "MC.ASSESSMENT.CONFLICTING_DUPLICATE" : outcome?.Reason ??
            $"MC.ASSESSMENT.{category.ToString().ToUpperInvariant()}";
        if (c is not null && !input.IsConflict)
            MarketConditionTelemetry.RecordFailure(category, reason, c.TargetHorizon,
                Math.Max(0, (clock.GetUtcNow().UtcDateTime - c.RequestedAtUtc).TotalMilliseconds));
        var failed = CreateFailedEvent(c, category, reason, clock);
        if (outcome?.Message is { } message) failed = failed with { ErrorMessage = message };
        return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(failed);
    }

    /// <summary>Creates a typed domain failure without saving Function state.</summary>
    static MarketConditionAssessmentFailedEvent CreateFailedEvent(ExecuteMarketConditionAssessmentCommand? c, MarketConditionFailureCategory category, string reason, TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, MarketConditionAssessmentFailedEvent.Verb, c?.EntityId.Format() ?? ""),
            Id = Guid.NewGuid(), EntityId = c?.WorkflowEntityId ?? default, WorkflowId = c?.WorkflowId ?? default,
            CommandId = c?.CommandId ?? Guid.Empty, InputWorkflowRevision = c?.InputWorkflowRevision ?? 0,
            ErrorDate = now, ReceivedOn = now, ErrorCode = MarketConditionAssessmentFailedEvent.ErrorId,
            ErrorType = ErrorType.Command, ErrorData = reason, ErrorMessage = $"Market assessment failed: {category}.",
            EventSource = $"{ExecuteMarketConditionAssessmentCommand.Actor}Actor", AggregateId = c?.EntityId.Format() ?? "",
            CommandName = nameof(ExecuteMarketConditionAssessmentCommand), RouteTo = c?.RouteTo.ToString() ?? "",
            CorrelationId = c?.CorrelationId ?? Guid.Empty, CausationId = c?.CausationId ?? Guid.Empty,
            PipelineStage = StrategyWorkflowStage.MarketCondition, FailureCategory = category,
            ExpiresAtUtc = c?.ExpiresAtUtc ?? default, ParameterPayloadSha256 = c?.ParameterPayloadSha256 ?? "", ProcessingStarted = c?.RequestedAtUtc ?? now
        };
    }
}
