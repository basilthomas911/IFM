using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// Client implementation for trade-related queries using an <see cref="IQueryServiceApi"/>.
/// </summary>
public class OptionTradeQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ITradeQueryApi
{
    /// <summary>
    /// Return trade history for selected trade order
    /// </summary>
    public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
    {
        var entityId = new GetTradeHistoryParameter(orderId);
        GetTradeHistoryQuery query = new(orderId)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradeHistoryQuery.Actor, GetTradeHistoryQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradeHistoryQuery, TradeHistoryReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return option leg contract ids
    /// </summary>
    public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
    {
        var entityId = new GetOptionLegContractIdsParameter(tradeId);
        GetOptionLegContractIdsQuery query = new(tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionLegContractIdsQuery.Actor, GetOptionLegContractIdsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionLegContractIdsQuery, string[]>(query.Subject, query);
    }

    /// <summary>
    /// Return trade limit for selected trade
    /// </summary>
    public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
    {
        var entityId = new GetTradeLimitParameter(tradeId);
        GetTradeLimitQuery query = new(tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradeLimitQuery.Actor, GetTradeLimitQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradeLimitQuery, TradeLimitReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return trade type limit
    /// </summary>
    public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
    {
        var entityId = new GetTradeTypeLimitParameter(tradeId, tradeType);
        GetTradeTypeLimitQuery query = new(tradeId, tradeType)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradeTypeLimitQuery.Actor, GetTradeTypeLimitQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradeTypeLimitQuery, TradeTypeLimitReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return trade quantity
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
    {
        var entityId = new GetTradeQuantityParameter(tradeId);
        GetTradeQuantityQuery query = new(tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradeQuantityQuery.Actor, GetTradeQuantityQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradeQuantityQuery, ScalarReadModel<int>>(query.Subject, query);
    }

    /// <summary>
    /// Return option trade
    /// </summary>
    public async Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId)
    {
        var entityId = new GetOptionTradeParameter(orderId, tradeId);
        GetOptionTradeQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeQuery.Actor, GetOptionTradeQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionTradeQuery, OptionTradeReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return option trade spread data
    /// </summary>
    public async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        var entityId = new GetOptionTradeSpreadDataParameter(orderId, tradeId, tradeType, valueDate);
        GetOptionTradeSpreadDataQuery query = new(orderId, tradeId, tradeType, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeSpreadDataQuery.Actor, GetOptionTradeSpreadDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionTradeSpreadDataQuery, OptionTradeSpreadsDataModel>(query.Subject, query);
    }

    /// <summary>
    /// Return option trade spread bar data
    /// </summary>
    public async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, System.DateTime startDate, System.DateTime endDate)
    {
        var entityId = new GetOptionTradeSpreadBarDataParameter(orderId, tradeId, tradeType, valueDate, startDate, endDate);
        GetOptionTradeSpreadBarDataQuery query = new(orderId, tradeId, tradeType, valueDate, startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradeSpreadBarDataQuery.Actor, GetOptionTradeSpreadBarDataQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionTradeSpreadBarDataQuery, OptionTradeSpreadBarsDataModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return option trades
    /// </summary>
    public async Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId)
    {
        var entityId = new GetOptionTradesParameter(orderId);
        GetOptionTradesQuery query = new(orderId)
        {
            Subject = new ActorSubject(ActorType.Query, GetOptionTradesQuery.Actor, GetOptionTradesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetOptionTradesQuery, OptionTradeReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return trade positions
    /// </summary>
    public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
    {
        var entityId = new GetTradePositionsParameter(orderId, tradeId);
        GetTradePositionsQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePositionsQuery.Actor, GetTradePositionsQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePositionsQuery, TradePositionReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return trade position by params
    /// </summary>
    public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        var entityId = new GetTradePositionParameter(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
        GetTradePositionQuery query = new(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePositionQuery.Actor, GetTradePositionQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePositionQuery, TradePositionReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor trade price
    /// </summary>
    public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
    {
        var entityId = new GetIronCondorTradePriceParameter(tradeId, valueDate);
        GetIronCondorTradePriceQuery query = new(tradeId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorTradePriceQuery.Actor, GetIronCondorTradePriceQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetIronCondorTradePriceQuery, TradePriceReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return trade plan summary
    /// </summary>
    public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync( int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new GetTradePlanSummaryParameter(orderId, tradeId, valueDate);
        GetTradePlanActionQuery query = new( orderId, tradeId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlanActionQuery.Actor, GetTradePlanActionQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlanActionQuery, TradePlanActionReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return trade position trade types
    /// </summary>
    public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        var entityId = new GetTradePositionTradeTypesParameter(orderId, tradeId, valueDate, daysToExpiry, tradeStatus);
        GetTradePositionTradeTypesQuery query = new(orderId, tradeId, valueDate, daysToExpiry, tradeStatus)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePositionTradeTypesQuery.Actor, GetTradePositionTradeTypesQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePositionTradeTypesQuery, string[]>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor MDI limit
    /// </summary>
    public async Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new GetIronCondorMDILimitParameter(orderId, tradeId, valueDate);
        GetIronCondorMDILimitQuery query = new(orderId, tradeId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorMDILimitQuery.Actor, GetIronCondorMDILimitQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetIronCondorMDILimitQuery, IronCondorMDILimitDataModel>(query.Subject, query);
    }
}
