using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
/// <summary>Owns activation, portfolio, fund, and policy initialization for Trade Selection.</summary>
public static class StartTradeSelectionPipeline
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Binding resolution failures become a typed failed start result instead of aborting the workflow actor.")]
    public static async ValueTask<PipelineStartResult<TradeSelectionPipelineInitialization>> StartPipelineAsync(IntrinsicTimeStrategyWorkflowView view, IIntrinsicTimeStrategyWorkflowRealtimeContext context, Guid causationId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(view);
        try
        {
            if (view.SelectionBinding is { } existing)
            {
                TradeSelectionContracts.ValidateBinding(existing);
                return PipelineStartResult<TradeSelectionPipelineInitialization>.Started(new(existing));
            }

            var horizon = view.TriggerEvent.EntityId.TimePeriod;
            var activationRef = context.Options.Activations.SingleOrDefault(value => value.Horizon == horizon);
            if (activationRef is null)
                return PipelineStartResult<TradeSelectionPipelineInitialization>.Failed("TS.INIT.ACTIVATION_MISSING", "ConfigurationUnavailable", "No exact Trade Selection activation is pinned for the triggering horizon.");
            var activation = await context.ConfigurationDb.GetEffectiveTradeSelectionActivationAsync(activationRef.Id, activationRef.Version, activationRef.PayloadSha256, view.StartedAtUtc).ConfigureAwait(false);
            var nextRevision = view.WorkflowRevision + 1;
            var binding = await new StrategySelectionUniverseResolver(context.ConfigurationDb).ResolveAsync(activation, view.WorkflowId.Value, nextRevision, view.CorrelationId, view.StartedAtUtc, view.ExpiresAtUtc, DateOnly.FromDateTime(view.TriggerEvent.CreatedOn)).ConfigureAwait(false);
            TradeSelectionContracts.ValidateBinding(binding);
            return PipelineStartResult<TradeSelectionPipelineInitialization>.Started(new(binding));
        }
        catch (Exception exception)
        {
            if (context.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
                global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write32(context.Logger, exception, nameof(StartTradeSelectionPipeline), nameof(StartPipelineAsync), view.WorkflowId);
            return PipelineStartResult<TradeSelectionPipelineInitialization>.Failed("TS.INIT.EXCEPTION", "InitializationFailed", PipelineExceptionDiagnostics.Summary("Trade Selection initialization failed", exception), PipelineExceptionDiagnostics.Create(exception, new Dictionary<string, string> (StringComparer.Ordinal){ ["WorkflowId"] = view.WorkflowId.ToString(), ["TargetHorizon"] = view.TriggerEvent.EntityId.TimePeriod.ToString(), ["CausationId"] = causationId.ToString() }));
        }
    }
}
