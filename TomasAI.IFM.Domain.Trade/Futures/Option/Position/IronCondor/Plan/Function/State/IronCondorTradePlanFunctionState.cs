using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;

/// <summary>Holds the single latest source snapshot while processing one monitoring request.</summary>
/// <remarks>The repository loads at most one full snapshot. No rolling plan collection is maintained.</remarks>
public sealed class IronCondorTradePlanFunctionState
    : BaseEventSourceActorState<IronCondorTradePlanFunctionState>,
      IEventSourceFunctionState<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand,
          IronCondorTradePlanUpdatedEvent>
{
    Guid activeCommandId;
    IronCondorTradePlanUpdatedEvent? latestTradePlanEvent;

    public override ActorThreadId Id { get; set; } = default!;
    public IronCondorTradePlanUpdatedEvent? CompletedEvent =>
        latestTradePlanEvent?.CommandId == activeCommandId ? latestTradePlanEvent : null;
    public bool IsCompleted => CompletedEvent is not null;
    /// <summary>Gets the revision from the single loaded source snapshot, independent of Scylla projection success.</summary>
    /// <summary>Gets the single loaded source payload for revision, stop and obsolete-observation guards.</summary>
    public StrategyTradePlanSnapshot? LatestSourceTradePlan => latestTradePlanEvent?.Plan;
    public long LatestPlanRevision => latestTradePlanEvent?.Plan.PlanRevision ?? 0;
    /// <summary>Gets the single loaded snapshot for accepted monitoring thresholds; no historical plan collection is retained.</summary>
    public IronCondorTradePlanSnapshot? LatestTradePlanSnapshot => latestTradePlanEvent?.Plan.IronCondorTradePlanSnapshot;

    /// <summary>Associates the loaded snapshot with the current request without loading or retaining any history.</summary>
    /// <param name="request">The current calculation request.</param><returns>This request-local state.</returns>
    public IronCondorTradePlanFunctionState Prepare(UpdateIronCondorTradePlanCommand request)
    {
        activeCommandId = request.CommandId;
        return this;
    }

    /// <summary>Checks whether this request already produced the loaded source snapshot.</summary>
    /// <param name="request">The calculation request.</param><returns>Whether its captured inputs match.</returns>
    public bool Matches(UpdateIronCondorTradePlanCommand request) =>
        CompletedEvent?.RequestFingerprint == request.Fingerprint();

    /// <summary>Applies the new snapshot event for source persistence.</summary>
    /// <param name="completedEvent">The computed snapshot event.</param><param name="request">Its originating request.</param>
    /// <returns>Whether the snapshot was accepted.</returns>
    public bool TryComplete(IronCondorTradePlanUpdatedEvent completedEvent, UpdateIronCondorTradePlanCommand request) =>
        !IsCompleted && Update(completedEvent, request);

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        IronCondorTradePlanUpdatedEvent snapshot => ApplySnapshot(snapshot),
        _ => false
    };

    /// <summary>Replaces the current snapshot with the event's full payload; no earlier plans are required.</summary>
    /// <param name="snapshot">The loaded or newly calculated snapshot event.</param><returns>True after applying the snapshot.</returns>
    bool ApplySnapshot(IronCondorTradePlanUpdatedEvent snapshot)
    {
        latestTradePlanEvent = snapshot;
        return true;
    }
}
