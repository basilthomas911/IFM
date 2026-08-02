using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event.Extensions;

internal static class FuturesEodDataEventExtensions
{
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
