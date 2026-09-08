using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function;

/// <summary>Builds completed Regime Discovery events through the Function event map.</summary>
public static class CompleteRegimeDiscoveryPipeline
{
    /// <summary>Wraps a typed successful calculation in a completed Function event candidate.</summary>
    /// <param name="input">The decoded request and successful calculation outcome selected by the event map.</param>
    /// <returns>A completed Function result containing a versioned typed result envelope.</returns>
    /// <remarks>The returned event has not yet been projected or persisted.</remarks>
    public static FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent> Complete(
        this FunctionEventContext<ExecuteRegimeDiscoveryPipelineCommand> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Request ?? throw new InvalidOperationException("Completion requires a decoded request.");
        if (input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed && input.Outcome is RegimeDiscoveryPipelineCompletedEvent committed)
            return FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>.Complete(committed);
        var outcome = input.Outcome as RegimeDiscoveryExecutionCompleted
            ?? throw new InvalidOperationException("Completion requires a successful Regime Discovery outcome.");
        var completed = new RegimeDiscoveryPipelineCompletedEvent
        {
            Subject = new ActorSubject(ActorType.Function, ExecuteRegimeDiscoveryPipelineCommand.Actor,
                RegimeDiscoveryPipelineCompletedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(outcome.Result.ProducedAtUtc, TimeSpan.Zero)),
            EntityId = command.WorkflowEntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = $"{ExecuteRegimeDiscoveryPipelineCommand.Actor}Actor",
            ReceivedOn = outcome.Result.ProducedAtUtc,
            WorkflowId = command.WorkflowId,
            InputWorkflowRevision = command.InputWorkflowRevision,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            PipelineStage = StrategyWorkflowStage.RegimeDiscovery,
            Result = StrategyStageResultEnvelope.CreateRegime(outcome.Result),
            CompletedAtUtc = outcome.Result.ProducedAtUtc,
            ExpiresAtUtc = command.ExpiresAtUtc,
            ParameterPayloadSha256 = command.ParameterPayloadSha256,
            SignalSnapshotId = outcome.SnapshotId
        };
        return FunctionResult<RegimeDiscoveryPipelineCompletedEvent, RegimeDiscoveryPipelineFailedEvent>
            .Complete(completed);
    }

}
