using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class TradePlanQueryApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ITradePlanQueryApi
{
    /// <summary>
    /// Return last iron condor stop loss limit for the specified order/trade.
    /// </summary>
    public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetIronCondorStopLossLimitAsync(int orderId, int tradeId)
    {
        var entityId = new GetStopLossLimitParameter(orderId, tradeId);
        GetStopLossLimitQuery query = new(orderId, tradeId)
        {
            Subject = new ActorSubject(ActorType.Query, GetStopLossLimitQuery.Actor, GetStopLossLimitQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetStopLossLimitQuery, TradePlanStopLossLimitReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return a range of iron condor forward loss ratios for the specified date range.
    /// </summary>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetIronCondorTradePlanForwardLossRatiosAsync(DateOnly startDate,DateOnly endDate)
    {
        var entityId = new GetTradePlanForwardLossRatiosParameter(startDate, endDate);
        GetTradePlanForwardLossRatiosQuery query = new(startDate, endDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlanForwardLossRatiosQuery.Actor, GetTradePlanForwardLossRatiosQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlanForwardLossRatiosQuery, TradePlanForwardLossRatioReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor forward loss ratio for the specified value date.
    /// </summary>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatioAsync(System.DateOnly valueDate)
    {
        var entityId = new GetTradePlanForwardLossRatioParameter(valueDate);
        GetTradePlanForwardLossRatioQuery query = new(valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlanForwardLossRatioQuery.Actor, GetTradePlanForwardLossRatioQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlanForwardLossRatioQuery, TradePlanForwardLossRatioReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor trade plans for the specified order/trade and value date.
    /// Map returned TradePlanReadModel objects to IronCondorTradePlanReadModel.
    /// </summary>
    public async Task<ServiceResult<TradePlanReadModel[]>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new GetTradePlansParameter(orderId, tradeId, valueDate);
        GetTradePlansQuery query = new(orderId, tradeId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlansQuery.Actor, GetTradePlansQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlansQuery, TradePlanReadModel[]>(query.Subject, query);
    }

    /// <summary>
    /// Return iron condor forward delta for the specified value date and trade type.
    /// </summary>
    public async Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
    {
        var entityId = new GetIronCondorForwardDeltaParameter(vixContractId, valueDate, tradeType, riskPositionType);
        GetIronCondorForwardDeltaQuery query = new(vixContractId, valueDate, tradeType, riskPositionType)
        {
            Subject = new ActorSubject(ActorType.Query, GetIronCondorForwardDeltaQuery.Actor, GetIronCondorForwardDeltaQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetIronCondorForwardDeltaQuery, IronCondorForwardDeltaDataModel>(query.Subject, query);
    }

    /// <summary>
    /// Retrieves the forward loss limit type for a specific trade plan based on the provided parameters.
    /// </summary>
    /// <remarks>This method queries the forward loss limit type for a trade plan using the provided
    /// identifiers and parameters. Ensure that the parameters are valid and correspond to an existing trade
    /// plan.</remarks>
    /// <param name="orderId">The unique identifier of the order associated with the trade plan.</param>
    /// <param name="tradeId">The unique identifier of the trade within the order.</param>
    /// <param name="valueDate">The value date for which the forward loss limit type is being queried.</param>
    /// <param name="tradeType">The type of trade for which the forward loss limit type is being queried.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing a <see cref="TradePlanForwardLossLimitReadModel"/> that represents
    /// the forward loss limit type for the specified trade plan.</returns>
    public async Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetForwardLossLimitTypeAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType)
    {
        var entityId = new GetTradePlanForwardLossLimitParameter(orderId, tradeId, tradeType, valueDate);
        GetTradePlanForwardLossLimitQuery query = new(orderId, tradeId, tradeType, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlanForwardLossLimitQuery.Actor, GetTradePlanForwardLossLimitQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlanForwardLossLimitQuery, TradePlanForwardLossLimitReadModel>(query.Subject, query);
    }

    /// <summary>
    /// Retrieves a collection of trade plans associated with the specified order, trade, and value date.
    /// </summary>
    /// <remarks>The method queries the underlying service to retrieve trade plans based on the provided
    /// identifiers and value date. Ensure that the <paramref name="orderId"/> and <paramref name="tradeId"/> correspond
    /// to valid entities in the system.</remarks>
    /// <param name="orderId">The unique identifier of the order for which trade plans are requested.</param>
    /// <param name="tradeId">The unique identifier of the trade for which trade plans are requested.</param>
    /// <param name="valueDate">The value date used to filter the trade plans.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
    /// object wrapping an array of <see cref="TradePlanReadModel"/> instances representing the trade plans.</returns>
    public async Task<ServiceResult<TradePlanReadModel[]>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        var entityId = new GetTradePlansParameter(orderId, tradeId, valueDate);
        GetTradePlansQuery query = new(orderId, tradeId, valueDate)
        {
            Subject = new ActorSubject(ActorType.Query, GetTradePlansQuery.Actor, GetTradePlansQuery.Verb, entityId.Format()),
        };
        return await RequestAsync<GetTradePlansQuery, TradePlanReadModel[]>(query.Subject, query);
    }
}
