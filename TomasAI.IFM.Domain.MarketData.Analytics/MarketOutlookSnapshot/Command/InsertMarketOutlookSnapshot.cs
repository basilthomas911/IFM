using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command;

public static class InsertMarketOutlookSnapshot
{
    public static ServiceResult<GuidResult> Execute(
        this InsertMarketOutlookSnapshotCommand command,
        MarketOutlookSnapshotCommandState state)
    {
        var inserted = new MarketOutlookSnapshotInsertedEvent
        {
            Subject = new(ActorType.Event, MarketOutlookSnapshotInsertedEvent.Actor,
                MarketOutlookSnapshotInsertedEvent.Verb, command.EntityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = DateTime.UtcNow,
            MarketOutlook = command.MarketOutlook
        };
        return state.Update(inserted, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(command.ErrorCode,
                $"{command.CommandName}: unable to apply snapshot inserted event");
    }
}
