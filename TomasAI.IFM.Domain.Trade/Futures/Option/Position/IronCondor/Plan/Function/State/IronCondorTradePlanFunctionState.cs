using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;

public sealed class IronCondorTradePlanFunctionState
    : BaseEventSourceActorState<IronCondorTradePlanFunctionState>,
      IEventSourceFunctionState<IronCondorTradePlanFunctionState, UpdateIronCondorTradePlanCommand,
          IronCondorTradePlanUpdatedEvent>
{
    Guid _activeCommandId;
    IronCondorTradePlanUpdatedEvent? _latest;

    public override ActorThreadId Id { get; set; } = default!;
    public IronCondorTradePlanUpdatedEvent? CompletedEvent =>
        _latest?.CommandId == _activeCommandId ? _latest : null;
    public StrategyTradePlanSnapshot? PreviousPlan => _latest?.Plan;
    public bool IsCompleted => CompletedEvent is not null;
    public long LastPersistedEventId { get; private set; }

    public IronCondorTradePlanFunctionState Prepare(UpdateIronCondorTradePlanCommand request)
    {
        _activeCommandId = request.CommandId;
        return this;
    }

    public bool Matches(UpdateIronCondorTradePlanCommand request) =>
        CompletedEvent?.RequestFingerprint == request.Fingerprint();

    public bool TryComplete(IronCondorTradePlanUpdatedEvent completedEvent,
        UpdateIronCondorTradePlanCommand request) =>
        !IsCompleted && Update(completedEvent, request);

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not IronCondorTradePlanUpdatedEvent completed) return false;
        _latest = completed;
        LastPersistedEventId = Math.Max(LastPersistedEventId, completed.EventId);
        return true;
    }
}
