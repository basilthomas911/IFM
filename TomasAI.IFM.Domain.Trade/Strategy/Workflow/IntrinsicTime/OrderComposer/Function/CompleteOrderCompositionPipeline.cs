using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;

/// <summary>Builds typed completions and observes committed or replayed results through the event map.</summary>
public static class CompleteOrderCompositionPipeline
{
    /// <summary>Creates a calculated completion or records a successful lifecycle observation.</summary>
    public static FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent> Complete(
        this FunctionEventContext<ExecuteOrderCompositionPipelineCommand> input, TimeProvider clock)
    {
        var c = input.Request ?? throw new ArgumentException("Completion requires its command.");
        if (input.Outcome is OrderCompositionFunctionCompletedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
        {
            if (input.Phase == FunctionEventPhase.Replayed) OrderCompositionTelemetry.Replay();
            else OrderCompositionTelemetry.Record(OrderCompositionContracts.ReadResult(committed.Result),
                Math.Max(0, (clock.GetUtcNow().UtcDateTime - c.RequestedAtUtc).TotalMilliseconds));
            return FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>.Complete(committed);
        }
        var result = input.Outcome as OrderCompositionResult ?? throw new ArgumentException("Completion requires a calculated composition.");

        CompositionRulesContract.Require(MessagePackBinarySerializer.MeasureContent(result) <= 524288,
            "OC.CONTRACT.PAYLOAD_SIZE");
        var completed=new OrderCompositionFunctionCompletedEvent
        {
            Subject=new(ActorType.Function,ExecuteOrderCompositionPipelineCommand.Actor,OrderCompositionFunctionCompletedEvent.Verb,c.EntityId.Format()),
            Id=result.ResultId,EntityId=c.WorkflowEntityId,CommandId=c.CommandId,AggregateId=c.EntityId.Format(),EventSource=c.EventSource,ReceivedOn=result.ProducedAtUtc,
            WorkflowId=c.WorkflowId,InputWorkflowRevision=c.InputWorkflowRevision,CorrelationId=c.CorrelationId,CausationId=c.CausationId,PipelineStage=StrategyWorkflowStage.OrderComposition,
            Result=StrategyStageResultEnvelope.CreateComposition(result, 524288),
            CompletedAtUtc=result.ProducedAtUtc,ExpiresAtUtc=c.ExpiresAtUtc,ParameterPayloadSha256=c.CompositionBinding.BindingSha256,
            EvaluatedAtUtc=result.EvaluatedAtUtc,ValidUntilUtc=result.ValidUntilUtc ?? default,RequestFingerprint=c.Fingerprint()
        };
        CompositionRulesContract.Require(MessagePackBinarySerializer.MeasureContent(completed) <= 1048576 &&
            MessagePackBinarySerializer.MeasureEncoded(completed) <= 1048576,
            "OC.CONTRACT.PAYLOAD_SIZE");
        OrderCompositionContracts.ReadResult(completed.Result);
        return FunctionResult<OrderCompositionFunctionCompletedEvent,OrderCompositionFunctionFailedEvent>.Complete(completed);
    }
}
