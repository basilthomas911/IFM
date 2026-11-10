using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Command;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.State;

/// <summary>Owns the full durable opening intent and its reconciliation evidence.</summary>
public sealed class StrategyExecutionHandoffCommandState : BaseEventSourceActorState<StrategyExecutionHandoffCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public StrategyExecutionHandoffDefinition? Definition { get; private set; }

    /// <summary>Restores a complete snapshot without depending on earlier hot state.</summary>
    protected override bool Apply(IEvent source)
    {
        if (source is not StrategyExecutionHandoffStateSnapshottedEvent snapshot ||
            !IsComplete(snapshot))
            return false;
        Definition = snapshot.Definition;
        return true;
    }

    /// <summary>Rejects an incomplete replacement instead of silently losing recovery or duplicate evidence.</summary>
    public static bool IsComplete(StrategyExecutionHandoffStateSnapshottedEvent snapshot) =>
        snapshot.EntityId.IsValid && snapshot.Definition.Id == snapshot.EntityId && snapshot.Definition.Revision > 0 &&
        snapshot.Definition.IntentHash.Length == 64 && snapshot.Definition.Authorization.StateHash.Length == 64 &&
        snapshot.Definition.IntentHash == BeginStrategyExecutionHandoff.Fingerprint(snapshot.Definition) &&
        (snapshot.Definition.Phase < StrategyExecutionHandoffPhase.Opened ||
            snapshot.Definition.FinancialReceipt is not null && snapshot.Definition.FundEvidence is not null &&
            (snapshot.Definition.Execution is not null || snapshot.Definition.Phase == StrategyExecutionHandoffPhase.Expired &&
                snapshot.Definition.FinancialReceipt.ProjectionFacts?.ExecutionCapacity?.Outcome.UnsubmittedExpiry == true));
}
