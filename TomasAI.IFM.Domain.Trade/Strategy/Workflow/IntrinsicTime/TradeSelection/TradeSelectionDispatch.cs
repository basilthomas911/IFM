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
    public static ExecuteTradeSelectionPipelineCommand Create(IntrinsicTimeStrategyWorkflowView view,Guid sourceEventId)
    {
        var binding=view.SelectionBinding??throw new ArgumentException("Selection binding is missing; unbound historical workflows cannot continue.");
        var policy=TradeSelectionContracts.ValidateBinding(binding);
        var assessment=MarketConditionAssessmentContracts.ReadResult(view.MarketCondition.Result!);
        var id=new TradeSelectionExecutionId(view.EntityId,view.WorkflowId,view.WorkflowRevision);
        var commandId=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{view.WorkflowId}|{StrategyWorkflowStage.TradeSelection}|{view.WorkflowRevision}")).AsSpan(0,16));
        var command=new ExecuteTradeSelectionPipelineCommand
        {
            SchemaVersion=1,CommandId=commandId,Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,ExecuteTradeSelectionPipelineCommand.Verb,id.Format()),
            EntityId=id,InputWorkflowRevision=view.WorkflowRevision,WorkflowView=view with {SelectionDispatch=null},TriggerEvent=view.TriggerEvent,
            CorrelationId=view.CorrelationId,CausationId=sourceEventId,RequestedAtUtc=view.UpdatedAtUtc,EvaluatedAtUtc=view.UpdatedAtUtc,
            ExpiresAtUtc=new[]{view.ExpiresAtUtc,view.UpdatedAtUtc.AddMilliseconds(policy.MaximumExecutionMilliseconds),binding.ValidUntilUtc,assessment.Assessment.ValidUntilUtc!.Value}.Min(),
            SelectionBinding=binding,RegimeResultEnvelope=view.RegimeDiscovery.Result!,AssessmentResultEnvelope=view.MarketCondition.Result!
        };
        // Normalize historical trigger constructor defaults once, before persisting the dispatch.
        command=command.NormalizeContent();
        TradeSelectionContracts.ValidateRequest(command);return command;
    }
}
