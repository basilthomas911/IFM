using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;

/// <summary>Builds typed completions and observes committed or replayed results through the event map.</summary>
public static class CompleteTradeSelectionPipeline
{
    /// <summary>Creates a calculated completion or records a successful lifecycle observation.</summary>
    public static FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent> Complete(
        this FunctionEventContext<ExecuteTradeSelectionPipelineCommand> input, TimeProvider clock)
    {
        var c = input.Request ?? throw new ArgumentException("Completion requires its command.");
        if (input.Outcome is TradeSelectionFunctionCompletedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
        {
            if (input.Phase == FunctionEventPhase.Replayed) TradeSelectionTelemetry.Replay();
            else TradeSelectionTelemetry.Record(TradeSelectionContracts.ReadResult(committed.Result),
                Math.Max(0, (clock.GetUtcNow().UtcDateTime - c.RequestedAtUtc).TotalMilliseconds));
            return FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>.Complete(committed);
        }
        var result = input.Outcome as TradeSelectionResult ?? throw new ArgumentException("Completion requires a calculated selection.");
        var policy = TradeSelectionContracts.CommonPolicy(c.SelectionBinding);
        TradeSelectionContracts.Require(MessagePackBinarySerializer.MeasureContent(result) <= policy.MaximumResultPayloadBytes,
            "TS.CONTRACT.PAYLOAD_SIZE", "Selection result exceeds its content limit.");
        var completed=new TradeSelectionFunctionCompletedEvent
        {
            Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,TradeSelectionFunctionCompletedEvent.Verb,c.EntityId.Format()),
            Id=result.ResultId,EntityId=c.WorkflowEntityId,CommandId=c.CommandId,AggregateId=c.EntityId.Format(),EventSource=c.EventSource,ReceivedOn=result.ProducedAtUtc,
            WorkflowId=c.WorkflowId,InputWorkflowRevision=c.InputWorkflowRevision,CorrelationId=c.CorrelationId,CausationId=c.CausationId,PipelineStage=StrategyWorkflowStage.TradeSelection,
            Result=StrategyStageResultEnvelope.CreateSelection(result, policy.MaximumResultPayloadBytes),
            CompletedAtUtc=result.ProducedAtUtc,ExpiresAtUtc=c.ExpiresAtUtc,ParameterPayloadSha256=c.SelectionBinding.CommonPolicy.PayloadSha256,
            EvaluatedAtUtc=result.EvaluatedAtUtc,ValidUntilUtc=result.ValidUntilUtc,RequestFingerprint=c.Fingerprint()
        };
        TradeSelectionContracts.Require(MessagePackBinarySerializer.MeasureContent(completed) <= TradeSelectionContracts.MaximumTransportBytes &&
            MessagePackBinarySerializer.MeasureEncoded(completed) <= TradeSelectionContracts.MaximumTransportBytes,
            "TS.CONTRACT.PAYLOAD_SIZE", "Completed event exceeds the transport content limit.");
        TradeSelectionContracts.ReadResult(completed.Result);
        return FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>.Complete(completed);
    }
}
