using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed record TradeSelectionPipelineInitialization(TradeSelectionBinding Binding, int FundId);

/// <summary>Owns activation, portfolio, fund, and policy initialization for Trade Selection.</summary>
public static class StartTradeSelectionPipeline
{
    public static async ValueTask<PipelineStartResult<TradeSelectionPipelineInitialization>> StartPipelineAsync(
        IntrinsicTimeStrategyWorkflowView view,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context,
        Guid causationId)
    {
        try
        {
            if (view.SelectionBinding is { } existing)
            {
                TradeSelectionContracts.ValidateBinding(existing);
                return PipelineStartResult<TradeSelectionPipelineInitialization>.Started(
                    new(existing, view.FundId > 0 ? view.FundId : existing.PortfolioSnapshot.Fund.FundId));
            }
            var horizon = view.TriggerEvent.EntityId.TimePeriod;
            var activationRef = context.Options.Activations.SingleOrDefault(value => value.Horizon == horizon);
            if (activationRef is null)
                return PipelineStartResult<TradeSelectionPipelineInitialization>.Failed("TS.INIT.ACTIVATION_MISSING", "ConfigurationUnavailable",
                    "No exact Trade Selection activation is pinned for the triggering horizon.");
            var activation = await context.ConfigurationDb.ResolveTradeSelectionActivationAsync(
                activationRef.Id, activationRef.Version, activationRef.PayloadSha256, view.StartedAtUtc).ConfigureAwait(false);
            var nextRevision = view.WorkflowRevision + 1;
            var portfolio = await context.PortfolioQueries.ResolveForSelectionAsync(
                activation.PortfolioId, activation.FundId, view.TriggerEvent.CreatedOn.Year, horizon.ToString(),
                activation.InstrumentRoot, view.StartedAtUtc, view.WorkflowId.Value, nextRevision, causationId).ConfigureAwait(false);
            if (!portfolio.Success || portfolio.Value is null)
                return PipelineStartResult<TradeSelectionPipelineInitialization>.Failed("TS.INIT.PORTFOLIO_UNAVAILABLE", "PortfolioUnavailable",
                    string.IsNullOrWhiteSpace(portfolio.ErrorMessage) ? "Selection portfolio authority could not be resolved." : portfolio.ErrorMessage);
            var binding = await new TradeSelectionBindingResolver(context.ConfigurationDb).ResolveAsync(
                portfolio.Value, activation.SelectionPolicyReference,
                DateOnly.FromDateTime(view.TriggerEvent.CreatedOn)).ConfigureAwait(false);
            TradeSelectionContracts.ValidateBinding(binding);
            return PipelineStartResult<TradeSelectionPipelineInitialization>.Started(new(binding, portfolio.Value.Fund.FundId));
        }
        catch (Exception exception)
        {
            context.Logger.LogError(exception, "Trade Selection initialization failed for workflow {WorkflowId}", view.WorkflowId);
            return PipelineStartResult<TradeSelectionPipelineInitialization>.Failed("TS.INIT.EXCEPTION", "InitializationFailed",
                PipelineExceptionDiagnostics.Summary("Trade Selection initialization failed", exception),
                PipelineExceptionDiagnostics.Create(exception, new Dictionary<string, string>
                {
                    ["WorkflowId"] = view.WorkflowId.ToString(),
                    ["TargetHorizon"] = view.TriggerEvent.EntityId.TimePeriod.ToString(),
                    ["CausationId"] = causationId.ToString()
                }));
        }
    }
}
