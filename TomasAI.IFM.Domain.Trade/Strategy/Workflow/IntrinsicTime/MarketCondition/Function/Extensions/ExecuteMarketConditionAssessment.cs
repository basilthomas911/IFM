using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Extensions;

/// <summary>Calculates one assessment; the Function base owns transport and completed-state commit.</summary>
public static class ExecuteMarketConditionAssessment
{
    /// <summary>Captures and evaluates the one triggering horizon using the explicit Function context.</summary>
    public static async ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteAsync(
        this ExecuteMarketConditionAssessmentCommand c, IMarketConditionFunctionContext context, CancellationToken cancellationToken)
    {
        var stage = MarketConditionFailureCategory.RequiredInputInvalid;
        using var activity = MarketConditionTelemetry.Start("market-condition.assessment");
        activity?.SetTag("workflow.id", c.WorkflowId.ToString());
        activity?.SetTag("correlation.id", c.CorrelationId.ToString());
        try
        {
            var snapshot = await context.SnapshotProvider.CaptureAsync(c.ParameterSet, context.TimeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
            stage = MarketConditionFailureCategory.CalculationFailed;
            var result = new MarketConditionAssessmentCalculator().Calculate(c, snapshot, c.CommandId);
            var completed = new MarketConditionAssessmentCompletedEvent
            {
                Subject = new(ActorType.Function, ExecuteMarketConditionAssessmentCommand.Actor, MarketConditionAssessmentCompletedEvent.Verb, c.EntityId.Format()),
                Id = result.ResultId, EntityId = c.WorkflowEntityId, CommandId = c.CommandId, AggregateId = c.EntityId.Format(),
                EventSource = $"{ExecuteMarketConditionAssessmentCommand.Actor}Actor", ReceivedOn = result.EvaluatedAtUtc,
                WorkflowId = c.WorkflowId, InputWorkflowRevision = c.InputWorkflowRevision, CorrelationId = c.CorrelationId, CausationId = c.CausationId,
                PipelineStage = StrategyWorkflowStage.MarketCondition, Result = StrategyStageResultEnvelope.Create(result.ResultId,
                    nameof(MarketConditionAssessmentResult), 1, MessagePackSerializer.Serialize(result), snapshot.EvaluatedAtUtc, result.EvaluatedAtUtc),
                CompletedAtUtc = context.TimeProvider.GetUtcNow().UtcDateTime, ExpiresAtUtc = c.ExpiresAtUtc, ParameterPayloadSha256 = c.ParameterPayloadSha256,
                MarketConditionSnapshotId = snapshot.SnapshotId, EvaluatedAtUtc = result.EvaluatedAtUtc, ValidUntilUtc = result.Assessment.ValidUntilUtc,
                RequestFingerprint = c.Fingerprint(), Snapshot = snapshot
            };
            return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Complete(completed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TimeoutException) { throw; }
        catch (Exception exception)
        {
            context.Logger.LogError(exception, "Assessment execution failed at {Stage}, Workflow={WorkflowId}", stage, c.WorkflowId);
            var reason = $"MC.ASSESSMENT.{stage.ToString().ToUpperInvariant()}";
            MarketConditionTelemetry.RecordFailure(stage, reason, c.TargetHorizon, Math.Max(0, (context.TimeProvider.GetUtcNow().UtcDateTime - c.RequestedAtUtc).TotalMilliseconds));
            return FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>.Fail(CreateFailedEvent(c, stage, reason, context.TimeProvider));
        }
    }
    /// <summary>Creates a typed domain failure without saving Function state.</summary>
    public static MarketConditionAssessmentFailedEvent CreateFailedEvent(ExecuteMarketConditionAssessmentCommand? c, MarketConditionFailureCategory category, string reason, TimeProvider clock)
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
