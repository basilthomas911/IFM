using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function.State;

public sealed class VerticalSpreadTradePlanFunctionState
    : BaseEventSourceActorState<VerticalSpreadTradePlanFunctionState>,
      IEventSourceFunctionState<VerticalSpreadTradePlanFunctionState, UpdateVerticalSpreadTradePlanCommand,
          VerticalSpreadTradePlanUpdatedEvent>
{
    Guid _activeCommandId;
    VerticalSpreadTradePlanUpdatedEvent? _latest;

    public override ActorThreadId Id { get; set; } = default!;
    public VerticalSpreadTradePlanUpdatedEvent? CompletedEvent =>
        _latest?.CommandId == _activeCommandId ? _latest : null;
    public StrategyTradePlanSnapshot? PreviousPlan => _latest?.Plan;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public VerticalSpreadTradePlanFunctionState Prepare(UpdateVerticalSpreadTradePlanCommand request)
    {
        _activeCommandId = request.CommandId;
        return this;
    }

    public bool Matches(UpdateVerticalSpreadTradePlanCommand request) =>
        CompletedEvent?.RequestFingerprint == request.Fingerprint();

    public bool TryComplete(VerticalSpreadTradePlanUpdatedEvent completedEvent,
        UpdateVerticalSpreadTradePlanCommand request) =>
        !IsCompleted && Update(completedEvent, request);

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not VerticalSpreadTradePlanUpdatedEvent completed) return false;
        _latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
