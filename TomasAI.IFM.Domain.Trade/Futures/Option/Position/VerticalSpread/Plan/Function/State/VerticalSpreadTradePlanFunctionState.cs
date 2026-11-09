using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;
public sealed class VerticalSpreadTradePlanFunctionState : BaseEventSourceActorState<VerticalSpreadTradePlanFunctionState>, IEventSourceFunctionState<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanUpdatedEvent>
{
    Guid _activeCommandId;
    VerticalSpreadTradePlanUpdatedEvent? _latest;
    public override ActorThreadId Id { get; set; } = default!;
    public VerticalSpreadTradePlanUpdatedEvent? CompletedEvent => _latest?.CommandId == _activeCommandId ? _latest : null;
    public StrategyTradePlanSnapshot? PreviousPlan => _latest?.Plan;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public VerticalSpreadTradePlanFunctionState Prepare(UpdateVerticalSpreadTradePlanCommand request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _activeCommandId = request.CommandId;
        return this;
    }

    public bool Matches(UpdateVerticalSpreadTradePlanCommand request) => string.Equals(CompletedEvent?.RequestFingerprint, request.Fingerprint(), StringComparison.Ordinal);
    public bool TryComplete(VerticalSpreadTradePlanUpdatedEvent completedEvent, UpdateVerticalSpreadTradePlanCommand request) => !IsCompleted && Update(completedEvent, request);
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not VerticalSpreadTradePlanUpdatedEvent completed)
            return false;
        _latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
    /// <summary>Owns serialized committed source data; each restore creates independent request-local state.</summary>
    public TomasAI.IFM.Application.EventModelActor.ResidentFunctionCheckpoint<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanFunctionState>?
        CaptureResidentCheckpoint()
    {
        if (_latest is null || CommittedStreamVersion <= 0 || Events.Count != 0) return null;
        return TomasAI.IFM.Application.EventModelActor.ResidentFunctionCheckpoint<UpdateVerticalSpreadTradePlanCommand, VerticalSpreadTradePlanFunctionState>.Capture(
            _latest, CommittedStreamVersion, static (request, fact, version) =>
            {
                var restored = new VerticalSpreadTradePlanFunctionState { Id = request.Subject.ThreadId };
                restored.ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[] { fact });
                restored.RestoreCommittedStreamVersion(version);
                return restored.Prepare(request);
            });
    }
}
