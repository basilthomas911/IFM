using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function;

/// <summary>Builds typed completions and observes committed or replayed results through the event map.</summary>
public static class CompleteRiskManagementPipeline
{
    /// <summary>Creates a calculated completion or records a successful lifecycle observation.</summary>
    public static FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent> Complete(
        this FunctionEventContext<ExecuteRiskManagementPipelineCommand> input, TimeProvider clock)
    {
        var c = input.Request ?? throw new ArgumentException("Completion requires its command.");
        if (input.Outcome is RiskManagementFunctionCompletedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
        {
            return FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>.Complete(committed);
        }
        var result = input.Outcome as RiskAssessmentResult ?? throw new ArgumentException("Completion requires a calculated Risk assessment.");
        RiskContracts.ValidateResult(result);

        RiskUnitModel.Require(MessagePackBinarySerializer.MeasureContent(result) <= 524288,
            "RM.INPUT.PAYLOAD_SIZE");
        var completed=new RiskManagementFunctionCompletedEvent
        {
            Subject=new(ActorType.Function,ExecuteRiskManagementPipelineCommand.Actor,RiskManagementFunctionCompletedEvent.Verb,c.EntityId.Format()),
            Id=result.ResultId,EntityId=c.WorkflowEntityId,CommandId=c.CommandId,AggregateId=c.EntityId.Format(),EventSource=c.EventSource,ReceivedOn=result.ProducedAtUtc,
            WorkflowId=c.WorkflowId,InputWorkflowRevision=c.InputWorkflowRevision,CorrelationId=c.CorrelationId,CausationId=c.CausationId,PipelineStage=StrategyWorkflowStage.RiskManagement,
            Result=result,
            CompletedAtUtc=result.ProducedAtUtc,ExpiresAtUtc=c.ExpiresAtUtc,ParameterPayloadSha256=c.PolicyHash,
            EvaluatedAtUtc=result.EvaluatedAtUtc,ValidUntilUtc=result.ValidUntilUtc,RequestFingerprint=c.Fingerprint()
        };
        RiskUnitModel.Require(MessagePackBinarySerializer.MeasureContent(completed) <= 1048576 &&
            MessagePackBinarySerializer.MeasureEncoded(completed) <= 1048576,
            "RM.INPUT.PAYLOAD_SIZE");
        return FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>.Complete(completed);
    }
}
