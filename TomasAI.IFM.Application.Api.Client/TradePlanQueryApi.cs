using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.TradePlan.ViewModels;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// REST client for trade plan queries. Delegates to an <see cref="IQueryServiceApi"/> and uses
/// the URI paths defined in <see cref="TradePlanQueryUriPath"/>.
/// </summary>
public class TradePlanQueryApi(IQueryServiceApi querySvc) 
    : ITradePlanQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Return last iron condor stop loss limit for the specified order/trade.
    /// </summary>
    public async Task<ServiceResult<TradePlanStopLossLimitReadModel>> GetIronCondorStopLossLimitAsync(int orderId, int tradeId)
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetStopLossLimit, new GetStopLossLimitQuery(orderId, tradeId));

    /// <summary>
    /// Return a range of iron condor forward loss ratios for the specified date range.
    /// </summary>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel[]>> GetIronCondorTradePlanForwardLossRatiosAsync(DateOnly startDate,DateOnly endDate)
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetTradePlanForwardLossRatios, new GetTradePlanForwardLossRatiosQuery(startDate, endDate));

    /// <summary>
    /// Return iron condor forward loss ratio for the specified value date.
    /// </summary>
    public async Task<ServiceResult<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatioAsync(System.DateOnly valueDate)
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetTradePlanForwardLossRatio, new GetTradePlanForwardLossRatioQuery(valueDate));

    /// <summary>
    /// Return iron condor trade plans for the specified order/trade and value date.
    /// Map returned TradePlanReadModel objects to IronCondorTradePlanReadModel.
    /// </summary>
    public async Task<ServiceResult<TradePlanReadModel[]>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateOnly valueDate)
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetTradePlans, new GetTradePlansQuery(orderId, tradeId, valueDate));

    /// <summary>
    /// Return iron condor forward delta for the specified value date and trade type.
    /// </summary>
    public async Task<ServiceResult<IronCondorForwardDeltaDataModel>> GetIronCondorForwardDeltaAsync(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType)
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetIronCondorForwardDelta, new GetIronCondorForwardDeltaQuery(vixContractId, valueDate, tradeType, riskPositionType));

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
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetTradePlanForwardLossLimitType, new GetTradePlanForwardLossLimitQuery(orderId, tradeId, tradeType, valueDate));

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
        => await _querySvc.ExecuteQueryAsync(TradePlanQueryUriPath.GetTradePlans, new GetTradePlansQuery(orderId, tradeId, valueDate));
}
