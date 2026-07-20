using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

internal static class FuturesEodDataEventExtensions
{
    /// <summary>
    /// Sends a futures end-of-day data updated event to the actor network.
    /// </summary>
    internal static async ValueTask SendFuturesEodDataUpdatedEventAsync(
        this IEventActorContext context, FuturesEodDataInsertedEvent e)
    {
        var entityId = e.EntityId;
        FuturesEodDataUpdatedEvent updatedEvent = new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesEodDataUpdatedEvent.Actor, FuturesEodDataUpdatedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            CommandId = e.CommandId,
            AggregateId = e.AggregateId,
            EventSource = e.EventSource,
            ReceivedOn = e.ReceivedOn,
            FuturesEodData = e.FuturesEodData,
            UpdatedOn = DateTime.Now,
            UpdatedBy = e.UserName
        };
        await context.SendAsync<FuturesEodDataUpdatedEvent, FuturesEodDataId>(updatedEvent);
    }

    /// <summary>
    /// Queries the VIX futures EOD data actor for the specified contract and value date.
    /// Returns an empty array when no data is available.
    /// </summary>
    internal static async ValueTask<VixFuturesEodDataReadModel[]> GetVixFuturesEodDataAsync(
        this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var entityId = new GetVixFuturesEodDataParameter(contractId, valueDate);
        GetVixFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetVixFuturesEodDataQuery.Actor, GetVixFuturesEodDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetVixFuturesEodDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(query);
        return serviceResult?.Success == true && serviceResult.Value is not null
            ? serviceResult.Value
            : [];
    }
}
