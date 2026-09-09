using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Chooses notification recovery or expiry without replacing any financial request identity.</summary>
public static class FinancialWorkflowRecoveryModel
{
    public static ICommand? Create(WorkflowStrategyStateUpdatedEvent snapshot,DateTime now)
    {
        if(now.Kind!=DateTimeKind.Utc) throw new ArgumentException("Recovery clock must be UTC.",nameof(now));
        var view=snapshot.State;
        if(view.Status!=WorkflowStrategyMachineStatus.Started || view.CurrentStage!=StrategyWorkflowStage.RiskManagement ||
            snapshot.UpdatedAtUtc>now.AddSeconds(-15)) return null;
        // Previous exploratory execution checkpoints must remain visible for explicit reconciliation.
        // Recovery cannot complete, consume or resubmit those as though an execution owner existed.
        if(view.FinancialHandoff?.Phase is RiskFinancialHandoffPhase.ConsumePending or RiskFinancialHandoffPhase.Consumed
            or RiskFinancialHandoffPhase.Submitted or RiskFinancialHandoffPhase.Authorized) return null;
        var commandId=Guid.NewGuid();
        if(now>=view.ExpiresAtUtc)
            return new TimeoutRiskManagementCommand
            {
                CommandId=commandId,TimeoutId=commandId,EntityId=view.EntityId,WorkflowId=view.WorkflowId,
                ExpectedWorkflowRevision=view.WorkflowRevision,ExpectedStage=view.CurrentStage,TimedOutAtUtc=now,
                Subject=new(ActorType.Command,TimeoutRiskManagementCommand.Actor,TimeoutRiskManagementCommand.Verb,view.EntityId.Format())
            };
        return new RedispatchCurrentStrategyPipelineCommand
        {
            CommandId=commandId,EntityId=view.EntityId,WorkflowId=view.WorkflowId,
            ExpectedWorkflowRevision=view.WorkflowRevision,ExpectedStage=view.CurrentStage,
            RequestedAtUtc=now,RequestedBy="FinancialWorkflowRecovery",
            Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,RedispatchCurrentStrategyPipelineCommand.Verb,view.EntityId.Format())
        };
    }
}
