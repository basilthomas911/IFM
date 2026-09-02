using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;

public sealed class MarketOutlookSnapshotCommandState
    : BaseEventSourceActorState<MarketOutlookSnapshotCommandState>,
      IEventSourceActorState<MarketOutlookSnapshotCommandState>
{
    MarketOutlookReadModel? snapshot;

    public override ActorThreadId Id { get; set; } = default!;

    internal MarketOutlookReadModel? Snapshot => snapshot;

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not MarketOutlookSnapshotInsertedEvent inserted)
            return false;
        snapshot = inserted.MarketOutlook;
        return true;
    }
}
