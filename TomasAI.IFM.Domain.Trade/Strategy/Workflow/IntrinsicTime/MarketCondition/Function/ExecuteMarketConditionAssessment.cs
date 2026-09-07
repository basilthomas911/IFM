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

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function;

/// <summary>Captures one snapshot and invokes the calculation model; terminal construction belongs to the event map.</summary>
public static class ExecuteMarketConditionAssessment
{
    /// <summary>Evaluates the single triggering timeframe and dispatches its calculation outcome.</summary>
    public static async ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteAsync(
        this ExecuteMarketConditionAssessmentCommand c, IMarketConditionFunctionContext context,
        Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>,
            FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> dispatchEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(dispatchEvent);
        var stage = MarketConditionFailureCategory.RequiredInputInvalid;
        using var activity = MarketConditionTelemetry.Start("market-condition.assessment");
        activity?.SetTag("workflow.id", c.WorkflowId.ToString());
        activity?.SetTag("correlation.id", c.CorrelationId.ToString());
        MarketConditionExecutionCompleted outcome;
        try
        {
            var snapshot = await context.SnapshotProvider.CaptureAsync(c.ParameterSet, context.TimeProvider.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
            stage = MarketConditionFailureCategory.CalculationFailed;
            var result = context.CalculationModel.Calculate(c, snapshot, c.CommandId);
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc) throw new TimeoutException();
            outcome = new(result, snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (TimeoutException) { throw; }
        catch (Exception exception)
        {
            context.Logger.LogError(exception, "Assessment execution failed at {Stage}, Workflow={WorkflowId}", stage, c.WorkflowId);
            return dispatchEvent(new(typeof(MarketConditionAssessmentFailedEvent), c,
                new MarketConditionExecutionFailed(stage, $"MC.ASSESSMENT.{stage.ToString().ToUpperInvariant()}")));
        }
        return dispatchEvent(new(typeof(MarketConditionAssessmentCompletedEvent), c, outcome));
    }
}

/// <summary>Local calculation outcome, never a serialized actor message.</summary>
internal sealed record MarketConditionExecutionCompleted(MarketConditionAssessmentResult Result, MarketConditionAssessmentSnapshot Snapshot);

/// <summary>Local domain or transport failure, translated only through the event map.</summary>
internal sealed record MarketConditionExecutionFailed(MarketConditionFailureCategory Category, string Reason, string? Message = null);
