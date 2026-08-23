using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.QueryParameters;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;

public static class IEventActorContextExtensions
{

    internal static async ValueTask<YieldCurveRateReadModel> GetLastYieldCurveRateAsync(this IEventActorContext context)
    {
        var entityId = new GetLastYieldCurveRateParameter();
        GetLastYieldCurveRateQuery query = new(true)
        {
            Subject = new ActorSubject(ActorType.Query, GetLastYieldCurveRateQuery.Actor, GetLastYieldCurveRateQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastYieldCurveRateQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<YieldCurveRateReadModel, GetLastYieldCurveRateQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }

    internal static async ValueTask<FuturesContractV2ReadModel> GetCurrentlyTradedFuturesContractAsync(this IEventActorContext context, string symbol)
    {
        var entityId = new GetCurrentlyTradedFuturesContractParameter(symbol);
        GetCurrentlyTradedFuturesContractQuery query = new(symbol)
        {
            Subject = new ActorSubject(ActorType.Query, GetCurrentlyTradedFuturesContractQuery.Actor, GetCurrentlyTradedFuturesContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetCurrentlyTradedFuturesContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesContractV2ReadModel, GetCurrentlyTradedFuturesContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new()!;
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="futuresContract"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask StartFuturesTickDataStreamingAsync(this IEventActorContext commandApi, IEvent e, FuturesContractV2ReadModel futuresContract, FuturesDataId entityId)
    {
        var (valueDate, resetStream) = e switch
        {
            MarketDataFeedStartedCompleteEvent o => (o.ValueDate, o.ResetStream),
            MarketDataFeedResetCompleteEvent o => (o.ValueDate, true),
            _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
        };
        var result = await commandApi.StartFuturesTickDataStreamingAsync(
            futuresContract, valueDate, resetStream, entityId);
        if (!result.Success)
            throw new InvalidOperationException(
                $"Futures tick streaming was not accepted for '{futuresContract.ContractId}': {result.ErrorMessage}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="context"></param>
    /// <param name="e"></param>
    /// <param name="entityId"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static async ValueTask StartFuturesBarDataStreamingAsync(this IEventActorContext commandApi, IEvent e, FuturesBarDataStreamingId entityId)
    {
        var (futuresContracts, valueDate) = e switch
        {
            MarketDataFeedStartedCompleteEvent o => (o.FuturesContracts!, o.ValueDate),
            MarketDataFeedResetCompleteEvent o => (o.FuturesContracts!, o.ValueDate),
            _ => throw new InvalidOperationException($"Unsupported event type: {e.GetType().FullName}")
        };
        var result = await commandApi.StartFuturesBarDataStreamingAsync(
            futuresContracts, valueDate, entityId);
        if (!result.Success)
            throw new InvalidOperationException(
                $"Futures bar streaming was not accepted for value date '{valueDate:yyyy-MM-dd}': {result.ErrorMessage}");
    }


    internal static async ValueTask<OptionTradeReadModel> GetOptionTradeQueryAsync(this IEventActorContext context, int orderId, int tradeId)
    {
        var entityId = new GetOptionTradeParameter(orderId, tradeId);
        GetOptionTradeQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetOptionTradeQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<OptionTradeReadModel, GetOptionTradeQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }

    internal static async ValueTask<FuturesOptionContractReadModel> GetFuturesOptionContractAsync(this IEventActorContext context, string contractId)
    {
        var entityId = new GetFuturesOptionContractParameter(contractId);
        GetFuturesOptionContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesOptionContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }
    internal static async ValueTask<FuturesContractV2ReadModel> GetFuturesContractAsync(this IEventActorContext context, string contractId)
    {
        var entityId = new GetFuturesContractParameter(contractId);
        GetFuturesContractQuery query = new(contractId)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesContractQuery.Actor, GetFuturesContractQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesContractQuery.ErrorId,
            QueryParams = entityId.Format()
        };
        var serviceResult = await context.RequestAsync<FuturesContractV2ReadModel, GetFuturesContractQuery>(query);
        return (serviceResult.Success && serviceResult.Value is not null)
            ? serviceResult.Value
            : new();
    }



}
