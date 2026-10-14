using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.Persistence;

/// <summary>Interprets already accepted trade workflow facts without database access or rerunning a pipeline.</summary>
public static class TradeProjectionFacts
{
    /// <summary>Reads the accepted assessment and validates its authoritative snapshot identity.</summary>
    public static MarketConditionAssessmentResult Assessment(MarketConditionAssessmentCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var result = MarketConditionAssessmentContracts.ReadResult(completed.Result);
        if (!string.Equals(completed.Snapshot.ComputeHash(), result.SnapshotSha256, StringComparison.Ordinal)
            || !string.Equals(completed.Snapshot.PayloadSha256, result.SnapshotSha256, StringComparison.Ordinal))
            throw new ArgumentException("Assessment projection snapshot hash mismatch.", nameof(result));
        return result;
    }

    /// <summary>Reads the accepted composer result and requires matching workflow and input identities.</summary>
    public static OrderCompositionResult Composition(OrderCompositionFunctionCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var result = OrderCompositionContracts.ReadResult(completed.Result);
        if (completed.Id != result.ResultId || completed.WorkflowId != result.WorkflowId || completed.EntityId != result.EntityId
            || completed.InputWorkflowRevision != result.InputWorkflowRevision
            || !string.Equals(completed.RequestFingerprint, result.InputSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Composition projection identity mismatch.");
        return result;
    }

    /// <summary>Reads the accepted selector decision without making a new selection.</summary>
    public static TradeSelectionResult Selection(TradeSelectionFunctionCompletedEvent completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var result = TradeSelectionContracts.ReadResult(completed.Result);
        TradeSelectionContracts.Require(completed.WorkflowId == result.WorkflowId && completed.CommandId == result.InvocationId
            && completed.Id == result.ResultId && completed.EntityId == result.EntityId
            && completed.InputWorkflowRevision == result.InputWorkflowRevision && completed.RequestFingerprint.Length == 64,
            "TS.RESULT.INVALID", "Projection event identity mismatch.");
        return result;
    }

    /// <summary>Reads the accepted regime result and validates its source identities.</summary>
    public static RegimeDiscoveryResult Regime(RegimeDiscoveryPipelineCompletedEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = source.Result.ReadRegimeResult();
        if (source.WorkflowId != result.WorkflowId || source.EntityId != result.EntityId || source.SignalSnapshotId != result.SignalSnapshotId
            || source.WorkflowId.Value == Guid.Empty || source.CommandId == Guid.Empty || source.Id == Guid.Empty || source.InputWorkflowRevision <= 0)
            throw new InvalidDataException("Regime Discovery source and result identities must agree.");
        return result;
    }

    /// <summary>Requires a persisted workflow source whose accepted state has the same identity and revision.</summary>
    public static IntrinsicTimeStrategyWorkflowView Workflow(WorkflowStrategyStateUpdatedEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = source.State;
        if (state.EntityId != source.EntityId || state.WorkflowId != source.WorkflowId || state.WorkflowRevision != source.WorkflowRevision || source.EventId <= 0)
            throw new ArgumentException("Committed workflow source identity and state must match.", nameof(source));
        return state;
    }

    /// <summary>Builds the active workflow read value exclusively from the accepted state.</summary>
    public static ActiveIntrinsicTimeStrategyWorkflowReadModel? Active(WorkflowStrategyStateUpdatedEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = source.State;
        var iti = state.EntityId.ItiSignalEntityId;
        return state.Status == WorkflowStrategyMachineStatus.Started
            ? new(state.EntityId.Format(), state.WorkflowId, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod, state.CurrentStage,
                state.WorkflowRevision, source.EventId, 2, MessagePackSerializer.Serialize(state), state.StartedAtUtc, state.UpdatedAtUtc)
            : null;
    }

    /// <summary>Maps the accepted state-machine status to its workflow read-model status.</summary>
    public static StrategyWorkflowStatus Status(IntrinsicTimeStrategyWorkflowView state) => state.Status switch
    {
        WorkflowStrategyMachineStatus.Empty => StrategyWorkflowStatus.None,
        WorkflowStrategyMachineStatus.Started => StrategyWorkflowStatus.Running,
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowStatus.Completed,
        _ => StrategyWorkflowStatus.Stopped
    };

    /// <summary>Preserves an explicit outcome and otherwise derives it from the terminal accepted status.</summary>
    public static StrategyWorkflowOutcome Outcome(IntrinsicTimeStrategyWorkflowView state) => state.Outcome != StrategyWorkflowOutcome.None ? state.Outcome : state.Status switch
    {
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowOutcome.Completed,
        WorkflowStrategyMachineStatus.Failed => StrategyWorkflowOutcome.PipelineFailed,
        WorkflowStrategyMachineStatus.TimedOut => StrategyWorkflowOutcome.TimedOut,
        WorkflowStrategyMachineStatus.Cancelled => StrategyWorkflowOutcome.Cancelled,
        _ => StrategyWorkflowOutcome.None
    };
}
