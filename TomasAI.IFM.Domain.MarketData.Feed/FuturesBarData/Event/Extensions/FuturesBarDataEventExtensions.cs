using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event.Extensions;

internal static class FuturesBarDataEventExtensions
{
    /// <summary>
    /// Queries for the last futures tick data for the specified contract and value date.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="contractId">The contract identifier to query tick data for.</param>
    /// <param name="valueDate">The value date to query tick data for.</param>
    /// <returns>The last futures tick data, or <see langword="null"/> if none is found.</returns>
    internal static async ValueTask<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(
        this IEventActorContext context, string contractId, DateOnly valueDate)
    {
        var futuresTickData = default(FuturesTickDataV2ReadModel);
        var entityId = new GetLastFuturesTickDataParameter(contractId, valueDate);
        GetLastFuturesTickDataQuery query = new(contractId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastFuturesTickDataQuery.Actor, GetLastFuturesTickDataQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesTickDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesTickDataV2ReadModel, GetLastFuturesTickDataQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresTickData = serviceResult.Value;
        return futuresTickData;
    }

    /// <summary>
    /// Queries for the last futures trade signal for the specified symbol and value date.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the query.</param>
    /// <param name="symbol">The futures symbol to query the trade signal for.</param>
    /// <param name="valueDate">The value date to query the trade signal for.</param>
    /// <returns>The last futures trade signal, or <see langword="null"/> if none is found.</returns>
    internal static async ValueTask<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        this IEventActorContext context, string symbol, DateOnly valueDate)
    {
        var futuresTradeSignal = default(FuturesTradeSignalV2ReadModel);
        var entityId = new GetFuturesTradeSignalBySymbolParameter(symbol, valueDate);
        GetFuturesTradeSignalBySymbolQuery query = new(symbol, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesTradeSignalBySymbolQuery.Actor, GetFuturesTradeSignalBySymbolQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesTickDataQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<FuturesTradeSignalV2ReadModel, GetFuturesTradeSignalBySymbolQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            futuresTradeSignal = serviceResult.Value;
        return futuresTradeSignal;
    }


}
