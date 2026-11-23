using TomasAI.IFM.Domain.Trade.Shared;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
public static class TradeSelectionDispatch
{
    public static ExecuteTradeSelectionPipelineCommand Create(IntrinsicTimeStrategyWorkflowView view, Guid sourceEventId,
        Model.PreparedTradeSelectionConfiguration? prepared = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        var binding = view.SelectionBinding ?? throw new ArgumentException("Selection binding is missing; unbound historical workflows cannot continue.",nameof(view));
        // ValidateRequest checks the full binding after the deadline is derived.
        var policy = prepared?.Policy ?? TradeSelectionContracts.CommonPolicy(binding);
        var assessment = prepared is null ? MarketConditionAssessmentContracts.ReadResult(view.MarketCondition.Result!)
            : view.MarketCondition.Result!.AssessmentResult!;
        var id = new TradeSelectionExecutionId(view.EntityId, view.WorkflowId, view.WorkflowRevision);
        var commandId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{view.WorkflowId}|{StrategyWorkflowStage.TradeSelection}|{view.WorkflowRevision}")).AsSpan(0, 16));
        var command = new ExecuteTradeSelectionPipelineCommand
        {
            SchemaVersion = 1,
            CommandId = commandId,
            Subject = new(ActorType.Function, ExecuteTradeSelectionPipelineCommand.Actor, ExecuteTradeSelectionPipelineCommand.Verb, id.Format()),
            EntityId = id,
            InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view with
            {
                SelectionDispatch = null
            },
            TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId,
            CausationId = sourceEventId,
            RequestedAtUtc = view.UpdatedAtUtc,
            EvaluatedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = new[]
            {
                Runtime.WorkflowCalculationRuntime.EffectiveDeadline(view),
                view.UpdatedAtUtc.AddMilliseconds(policy.MaximumExecutionMilliseconds),
                binding.ValidUntilUtc
            }.Min(),
            SelectionBinding = binding,
            RegimeResultEnvelope = view.RegimeDiscovery.Result!,
            AssessmentResultEnvelope = view.MarketCondition.Result!
        };
        // The frozen evaluation must be valid when requested. Evidence expiry
        // limits permission to trade, not the budget to persist a NoTrade decision.
        TradeSelectionContracts.Require(command.EvaluatedAtUtc < assessment.Assessment.ValidUntilUtc,
            "TS.TIME.EXPIRED", "Assessment evidence expired before selection was requested.");
        // Normalize historical trigger constructor defaults once, before persisting the dispatch.
        if (prepared is null)
        {
            command = command.NormalizeContent();
            TradeSelectionContracts.ValidateRequest(command);
        }
        return command;
    }
}
