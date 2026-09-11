using System.Globalization;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Realtime;

/// <summary>Owns configuration initialization for one admitted Market Condition stage.</summary>
public static class StartMarketConditionPipeline
{
    public static async ValueTask<PipelineStartResult<WorkflowStrategyStateUpdatedEvent>> StartPipelineAsync(
        this WorkflowStrategyStateUpdatedEvent snapshot,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        var view = snapshot.State;
        try
        {
            if (view.AssessmentBinding is { } existing)
            {
                existing.Validate();
                return PipelineStartResult<WorkflowStrategyStateUpdatedEvent>.Started(snapshot);
            }
            if (context.TimeProvider.GetUtcNow().UtcDateTime >= view.ExpiresAtUtc)
                return PipelineStartResult<WorkflowStrategyStateUpdatedEvent>.Failed("MC.INIT.DEADLINE", "InitializationTimeout", "Market Condition initialization reached the workflow deadline.");
            var resolved = await context.ConfigurationDb.ResolveEffectiveMarketConditionAssessmentAsync(
                view.StartedAtUtc, context.Options.MarketConditionAssessmentProfileId, "ES",
                view.TriggerEvent.EntityId.TimePeriod).ConfigureAwait(false);
            if (resolved is null)
                return PipelineStartResult<WorkflowStrategyStateUpdatedEvent>.Failed("MC.INIT.CONFIGURATION_MISSING", "ConfigurationUnavailable",
                    "No published Market Condition assessment profile is effective for the workflow.");
            var binding = new MarketConditionAssessmentBinding { Parameters = resolved.ParameterSet, PayloadSha256 = resolved.PayloadSha256 };
            binding.Validate();
            return PipelineStartResult<WorkflowStrategyStateUpdatedEvent>.Started(snapshot with
            {
                State = view with
                {
                    AssessmentBinding = binding,
                    MarketCondition = view.MarketCondition with
                    {
                        ParameterSetId = resolved.ParameterSet.ParameterSetId,
                        ParameterSetVersion = resolved.ParameterSet.Version,
                        ParameterPayloadSha256 = resolved.PayloadSha256
                    }
                }
            });
        }
        catch (Exception exception)
        {
            context.Logger.LogError(exception, "Market Condition initialization failed for workflow {WorkflowId}", view.WorkflowId);
            return PipelineStartResult<WorkflowStrategyStateUpdatedEvent>.Failed("MC.INIT.EXCEPTION", "InitializationFailed",
                PipelineExceptionDiagnostics.Summary("Market Condition initialization failed", exception),
                PipelineExceptionDiagnostics.Create(exception, new Dictionary<string, string>
                {
                    ["WorkflowId"] = view.WorkflowId.ToString(),
                    ["TargetHorizon"] = view.TriggerEvent.EntityId.TimePeriod.ToString(),
                    ["StartedAtUtc"] = view.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                    ["ExpiresAtUtc"] = view.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)
                }));
        }
    }
}
