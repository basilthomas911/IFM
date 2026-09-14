using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function.State;

public sealed class FuturesTradePlanFunctionState
    : BaseEventSourceActorState<FuturesTradePlanFunctionState>,
      IEventSourceFunctionState<FuturesTradePlanFunctionState, UpdateFuturesTradePlanCommand,
          FuturesTradePlanUpdatedEvent>
{
    Guid _activeCommandId;
    FuturesTradePlanUpdatedEvent? _latest;

    public override ActorThreadId Id { get; set; } = default!;
    public FuturesTradePlanUpdatedEvent? CompletedEvent =>
        _latest?.CommandId == _activeCommandId ? _latest : null;
    public StrategyTradePlanSnapshot? PreviousPlan => _latest?.Plan;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public FuturesTradePlanFunctionState Prepare(UpdateFuturesTradePlanCommand request)
    {
        _activeCommandId = request.CommandId;
        return this;
    }

    public bool Matches(UpdateFuturesTradePlanCommand request) =>
        CompletedEvent?.RequestFingerprint == request.Fingerprint();

    public bool TryComplete(FuturesTradePlanUpdatedEvent completedEvent,
        UpdateFuturesTradePlanCommand request) =>
        !IsCompleted && Update(completedEvent, request);

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesTradePlanUpdatedEvent completed) return false;
        _latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
