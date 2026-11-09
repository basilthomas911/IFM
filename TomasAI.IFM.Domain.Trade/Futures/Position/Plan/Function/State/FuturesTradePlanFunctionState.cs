using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;
public sealed class FuturesTradePlanFunctionState : BaseEventSourceActorState<FuturesTradePlanFunctionState>, IEventSourceFunctionState<FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand, FuturesTradePlanUpdatedEvent>
{
    Guid _activeCommandId;
    FuturesTradePlanUpdatedEvent? _latest;
    public override ActorThreadId Id { get; set; } = default!;
    public FuturesTradePlanUpdatedEvent? CompletedEvent => _latest?.CommandId == _activeCommandId ? _latest : null;
    public StrategyTradePlanSnapshot? PreviousPlan => _latest?.Plan;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public FuturesTradePlanFunctionState Prepare(UpdateFuturesTradePlanCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _activeCommandId = request.CommandId;
        return this;
    }

    public bool Matches(UpdateFuturesTradePlanCommand request) => string.Equals(CompletedEvent?.RequestFingerprint, request.Fingerprint(), StringComparison.Ordinal);
    public bool TryComplete(FuturesTradePlanUpdatedEvent completedEvent, UpdateFuturesTradePlanCommand request) => !IsCompleted && Update(completedEvent, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesTradePlanUpdatedEvent completed)
            return false;
        _latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
    /// <summary>Owns serialized committed source data; each restore creates independent request-local state.</summary>
    public TomasAI.IFM.Application.EventModelActor.ResidentFunctionCheckpoint<UpdateFuturesTradePlanCommand, FuturesTradePlanFunctionState>?
        CaptureResidentCheckpoint()
    {
        if (_latest is null || CommittedStreamVersion <= 0 || Events.Count != 0) return null;
        return TomasAI.IFM.Application.EventModelActor.ResidentFunctionCheckpoint<UpdateFuturesTradePlanCommand, FuturesTradePlanFunctionState>.Capture(
            _latest, CommittedStreamVersion, static (request, fact, version) =>
            {
                var restored = new FuturesTradePlanFunctionState { Id = request.Subject.ThreadId };
                restored.ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[] { fact });
                restored.RestoreCommittedStreamVersion(version);
                return restored.Prepare(request);
            });
    }
}
