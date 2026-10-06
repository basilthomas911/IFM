using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

/// <summary>Handles one realtime EOD insertion notification.</summary>
public static class FuturesEodDataInserted
{
    /// <summary>Publishes the inserted EOD value to the actor-owned blackboard.</summary>
    public static async ValueTask ExecuteAsync(this FuturesEodDataInsertedEvent domainEvent, IFuturesEodDataRealtimeContext context)
    {
        if (CurrentFuturesEodCache.Shared.TryGet(domainEvent.EntityId.ContractId, domainEvent.EntityId.ValueDate, out var latest)
            && latest != domainEvent.FuturesEodData)
            return;
        context.BlackboardService.MarketDataFeed.FuturesEodData.Set(
            domainEvent.FuturesEodData.ContractId,
            domainEvent.FuturesEodData.ValueDate,
            domainEvent.FuturesEodData);
        // Live notification is explicitly distinct from persistence completion.
        if (CurrentFuturesEodCache.Shared.TryGet(domainEvent.EntityId.ContractId, domainEvent.EntityId.ValueDate, out var current)
            && current == domainEvent.FuturesEodData)
        {
            await context.SendAsync<FuturesEodDataUpdatedNotifyEvent, FuturesEodDataId>(new()
            {
                Subject = new(ActorType.Notify, FuturesEodDataUpdatedNotifyEvent.Actor, FuturesEodDataUpdatedNotifyEvent.Verb, domainEvent.EntityId.Format()),
                Id = domainEvent.Id, EntityId = domainEvent.EntityId, CommandId = domainEvent.CommandId,
                EventSource = nameof(FuturesEodDataInsertedEvent), ReceivedOn = DateTime.UtcNow,
                FuturesEodData = domainEvent.FuturesEodData, IsPersisted = false
            });
            if (domainEvent.FuturesEodData.Symbol == "ES")
            {
                var id = new MarketOutlookEntityId(domainEvent.EntityId.ContractId, domainEvent.EntityId.ValueDate);
                await context.SendAsync<MarketOutlookEodUpdatedRealtimeEvent, MarketOutlookEntityId>(new()
                {
                    Subject = new(ActorType.Realtime, MarketOutlookEodUpdatedRealtimeEvent.Actor, MarketOutlookEodUpdatedRealtimeEvent.Verb, id.Format()),
                    Id = domainEvent.Id, EntityId = id, CommandId = domainEvent.CommandId,
                    EventSource = nameof(FuturesEodDataInsertedEvent), ReceivedOn = DateTime.UtcNow,
                    FuturesEodData = domainEvent.FuturesEodData
                });
            }
        }
    }
}