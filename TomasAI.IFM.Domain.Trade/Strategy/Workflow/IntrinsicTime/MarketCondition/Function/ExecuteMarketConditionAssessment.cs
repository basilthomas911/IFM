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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Unexpected calculation failures are mapped to the typed assessment failure; requested cancellation propagates separately.")]
    public static async ValueTask<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteAsync(this ExecuteMarketConditionAssessmentCommand c, IMarketConditionFunctionContext context, Func<FunctionEventContext<ExecuteMarketConditionAssessmentCommand>, FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> dispatchEvent, CancellationToken cancellationToken)
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
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc)
                throw new TimeoutException();
            stage = MarketConditionFailureCategory.CalculationFailed;
            var result = context.CalculationModel.Calculate(c, snapshot, c.CommandId);
            cancellationToken.ThrowIfCancellationRequested();
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= c.ExpiresAtUtc)
                throw new TimeoutException();
            outcome = new(result, snapshot);
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (context.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write36(context.Logger, exception, nameof(ExecuteMarketConditionAssessment), nameof(ExecuteAsync), stage, c.WorkflowId);
            return dispatchEvent(new(typeof(MarketConditionAssessmentFailedEvent), c, new MarketConditionExecutionFailed(stage, $"MC.ASSESSMENT.{stage.ToString().ToUpperInvariant()}")));
        }

        return dispatchEvent(new(typeof(MarketConditionAssessmentCompletedEvent), c, outcome));
    }
}
