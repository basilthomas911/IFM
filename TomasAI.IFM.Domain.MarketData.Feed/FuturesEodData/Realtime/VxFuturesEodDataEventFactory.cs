using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Creates the existing storage lifecycle event for a resolved VX observation.</summary>
internal static class VxFuturesEodDataEventFactory
{
    internal static VixFuturesEodDataInsertedEvent Create(
        IEvent source,
        FuturesTickDataV2ReadModel tickData,
        FuturesSessionStatisticsSnapshot? sessionStatistics = null)
    {
        var entityId = new FuturesEodDataId(tickData.ContractId, tickData.ValueDate);
        return new VixFuturesEodDataInsertedEvent
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                FuturesEodDataRealtimeActor.ActorName,
                VixFuturesEodDataInsertedEvent.Verb,
                entityId.Format()),
            Id = Guid.NewGuid(),
            EntityId = entityId,
            CommandId = source.CommandId,
            AggregateId = source.AggregateId,
            EventSource = source.EventName,
            ReceivedOn = DateTime.UtcNow,
            VixFuturesTickData = tickData,
            SessionStatistics = sessionStatistics,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = source.UserName
        };
    }
}
