using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;

/// <summary>Rehydrates the latest complete stage of one exit decision.</summary>
public abstract class ExitPositionWorkflowCommandStateBase<TState> : BaseEventSourceActorState<TState>
    where TState : ExitPositionWorkflowCommandStateBase<TState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public ExitPositionWorkflowStartedEvent? Started { get; private set; }
    public ExitPositionWorkflowSnapshot? Snapshot { get; private set; }
    public Guid CurrentSourceEventId { get; private set; }

    internal bool ApplyCommittedSnapshot(IEvent source) => Apply(source);

    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ExitPositionWorkflowStartedEvent started when IsFullSnapshot(started):
                Started = started;
                Snapshot = new() { Started = started, Projection = ExitWorkflowSnapshotFacts.From(started) };
                break;
            case ExitPositionWorkflowLifecycleEvent lifecycle when IsFullSnapshot(lifecycle) &&
                (Started is null || Started.EntityId == lifecycle.Snapshot.Started.EntityId &&
                    Started.SourcePlanEventId == lifecycle.Snapshot.Started.SourcePlanEventId):
                Started = lifecycle.Snapshot.Started;
                Snapshot = lifecycle.Snapshot;
                break;
            default:
                return false;
        }
        CurrentSourceEventId = ExitWorkflowContinuation.SourceIdentity(domainEvent);
        return true;
    }

    static bool IsFullSnapshot(IEvent source) => source switch
    {
        ExitPositionWorkflowStartedEvent started => started.EntityId.IsValid && started.ExitPlan is not null,
        ExitPositionWorkflowLifecycleEvent { Snapshot.Started: { } started, Snapshot.Projection: { } projection } stage
            => stage.EntityId == started.EntityId && stage.EntityId == projection.WorkflowId &&
                started.StrategyKind == projection.StrategyKind && started.SourcePlanEventId == projection.SourcePlanEventId &&
                started.ExitPlan is not null && projection.ExitPlan is not null && projection.StageRevision > 1 &&
                (projection.State switch
                {
                    ExitPositionWorkflowState.OrderComposed => projection.Composition is not null && stage.Snapshot.CompositionReceiptId != Guid.Empty,
                    ExitPositionWorkflowState.RiskAccepted or ExitPositionWorkflowState.NoTradeOrders =>
                        projection.Composition is not null && projection.RiskDecision is not null &&
                        stage.Snapshot.CompositionReceiptId != Guid.Empty && stage.Snapshot.RiskReceiptId != Guid.Empty,
                    ExitPositionWorkflowState.Completed => projection.Composition is not null && projection.RiskDecision is not null &&
                        stage.Snapshot.CompositionReceiptId != Guid.Empty && stage.Snapshot.RiskReceiptId != Guid.Empty &&
                        stage.Snapshot.ExecutionAttemptId != Guid.Empty && stage.Snapshot.BindingCommandId != Guid.Empty,
                    ExitPositionWorkflowState.Failed => !string.IsNullOrWhiteSpace(stage.Snapshot.FailureCode),
                    _ => false
                }),
        _ => false
    };
}
