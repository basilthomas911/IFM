using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Client;

public class OptionTradeCommandApi(ICommandServiceApi commandSvc) : ITradeCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// fill spread trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> SnapshotAsync(int orderId, int tradeId)
        => await new SnapshotOptionTradeCommand(orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.Snapshot, e));

    /// <summary>
    /// delete trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteAsync(int orderId, int tradeId)
        => await new DeleteOptionTradeCommand(orderId, tradeId)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.Delete, e));

    /// <summary>
    /// place a trade order with trade broker
    /// </summary>
    /// <param name="tradeOrder"></param>
    /// <param name="optionTrade"></param>
    public async Task<ServiceResult<Guid>> PlaceOrderAsync(TradeOrderReadModel tradeOrder, OptionTradeReadModel optionTrade)
        => await new PlaceOptionTradeOrderCommand(tradeOrder, optionTrade)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionTradeUriPath.PlaceOrder, e));

    /// <summary>
    /// open option trade for trading
    /// </summary>
    /// <param name="tradeOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> OpenOptionTradeAsync(TradeOrderReadModel tradeOrder)
        => await new OpenOptionTradeCommand(tradeOrder)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.Open, e));

    /// <summary>
    /// close option trade for trading
    /// </summary>
    /// <param name="tradeOrder"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> CloseOptionTradeAsync(TradeOrderReadModel tradeOrder)
        => await new CloseOptionTradeCommand(tradeOrder)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.Close, e));

    /// <summary>
    /// insert option trade spread data
    /// </summary>
    /// <param name="optionTradeSpreadData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel optionTradeSpreadData)
        => await new InsertOptionTradeSpreadDataCommand(optionTradeSpreadData)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.InsertSpreadData, e));

    /// <summary>
    /// insert option trade spread bar data
    /// </summary>
    /// <param name="optionTradeSpreadBarData"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData)
        => await new InsertOptionTradeSpreadBarDataCommand(optionTradeSpreadBarData)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.InsertSpreadBarData, e));

    /// <summary>
    /// delete option trade spread bar data
    /// </summary>
    /// <param name="optionTradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> DeleteOptionTradeSpreadBarDataAsync(OptionTradeEntityId optionTradeId, TradeType tradeType, DateOnly valueDate)
        => await new DeleteOptionTradeSpreadBarDataCommand(optionTradeId, tradeType, valueDate)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.DeleteSpreadBarData, e));

    /// <summary>
    /// change short option leg data via web service
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeStatus"></param>
    /// <param name="assetPrice"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="optionLegData"></param>
    public async Task<ServiceResult<Guid>> ChangeOptionLegDataAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal assetPrice,
        double riskFreeRate,
        OptionTradeLegDataReadModel optionLegData)
        => await new ChangeOptionTradeLegDataCommand(orderId, tradeId, tradeType, valueDate, tradeStatus, assetPrice, riskFreeRate, optionLegData)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.ChangeLegData, e));

    /// <summary>
    /// change distribution statistics via web service call
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeStatus"></param>
    /// <param name="valueDate"></param>
    /// <param name="putSpreadDistribution"></param>
    /// <param name="callSpreadDistribution"></param>
    public async Task<ServiceResult<Guid>> ChangeDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
        => await new UpdateOptionTradeSpreadDistributionStatisticsCommand(orderId, tradeId, tradeType, tradeStatus, valueDate, 0, putSpreadDistribution, callSpreadDistribution)
        .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.ChangeDistributionStatistics, e));

    /// <summary>
    /// process end of day via web service call
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeStatus"></param>
    /// <param name="openPrice"></param>
    /// <param name="highPrice"></param>
    /// <param name="lowPrice"></param>
    /// <param name="closePrice"></param>
    /// <param name="volume"></param>
    /// <param name="reference"></param>
    public async Task<ServiceResult<Guid>> ProcessEndOfDayAsync(int fundId, int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, TradeStatus tradeStatus, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, int volume, string reference)
        => await new ProcessOptionTradeEndOfDayCommand(fundId, orderId, tradeId, tradeType, valueDate, tradeStatus, openPrice, highPrice, lowPrice, closePrice, volume, reference)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(OptionTradeUriPath.ProcessEndOfDay, e));

    /// <summary>
    /// update trade limit daily profit target
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradingDays"></param>
    /// <param name="maxTradingDays"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateTradeLimitDailyProfitTargetAsync(int orderId, int tradeId, int tradingDays, int maxTradingDays)
        => await new UpdateOptionTradeDailyProfitTargetCommand(orderId, tradeId, tradingDays, maxTradingDays)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.UpdateDailyProfitTarget, e));

    public async Task<ServiceResult<Guid>> DeleteAsync(int orderid)
        => await new DeleteOptionTradesCommand(orderid)
            .ExecuteAsync(e => _commandSvc.PostCommandAsync(OptionTradeUriPath.DeleteOptionTrades, e));

   
}
