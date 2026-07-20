using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Client;

public class TradePlanQueryApi(IQueryService querySvc) : ITradePlanQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "TradePlan";

    /// <summary>
    /// return 
    /// </summary>
    /// <param name="lossProbability"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<LossProbabilityDataModel>> GetLossProbabilityAsync(double lossProbability, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLossProbabilityQuery ( lossProbability, valueDate), _controller);

    /// <summary>
    /// return loss probability distribution from value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<LossProbabilityDistributionDataModel>> GetLossProbabilityDistributionAsync(DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLossProbabilityDistributionQuery(valueDate), _controller);

    /// <summary>
    /// return last trade plan stop loss limit
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetStopLossLimitAsync(int orderId, int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetStopLossLimitQuery (orderId, tradeId), _controller);

    /// <summary>
    /// return range of forward loss ratios
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossRatiosQuery(startDate, endDate), _controller);

    /// <summary>
    /// return forward loss ratio by value date
    /// </summary>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatioAsync(DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossRatioQuery(valueDate), _controller);

    /// <summary>
    /// return trade plan action for selected orderid/tradeid
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanActionAsync(int orderId, int tradeId, DateOnly valueDate) 
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanActionQuery( orderId, tradeId, valueDate), _controller);

    /// <summary>
    /// return trade planS for selected orderid/tradeid
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanReadModel[]>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlansQuery(orderId, tradeId, valueDate), _controller);

    /// <summary>
    /// return iron condor forward delta
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <param name="riskPositionType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
        => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorForwardDeltaQuery (vixContractId, valueDate, tradeType, riskPositionType), _controller);

    /// <summary>
    /// return  trade plan forward loss limit
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanForwardLossLimitQuery(orderId, tradeId, tradeType, valueDate), _controller);

    // --- Interface wrapper methods to match ITradePlanQueryApi expected names ---

    public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetIronCondorStopLossLimitAsync(int orderId, int tradeId)
        => await GetStopLossLimitAsync(orderId, tradeId);

    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetIronCondorTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate)
        => await GetTradePlanForwardLossRatiosAsync(startDate, endDate);

    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatioAsync(DateOnly valueDate)
        => await GetTradePlanForwardLossRatioAsync(valueDate);

    public async Task<ServiceResult<TradePlanReadModel[]>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
        => await GetTradePlansAsync(orderId, tradeId, valueDate);

    // Interface expects no vixContractId; forward with empty string
    public async Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
        => await GetIronCondorForwardDeltaAsync(string.Empty, valueDate, tradeType, riskPositionType);

    public async Task<ServiceResult<TradePlanForwardLossLimitReadModel>> GetForwardLossLimitTypeAsync(int OrderId, int TradeId, DateOnly ValueDate, TradeType TradeType)
        => await GetTradePlanForwardLossLimitAsync(OrderId, TradeId, TradeType, ValueDate);
}
