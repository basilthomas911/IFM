using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Events;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

namespace TomasAI.IFM.Models;

public class FundCommandModel : BaseModel<FundCommandModel>
{
    readonly IFundCommandApi _commandApi;
    readonly IFundRiskMarginUIEventConsumer _fundRiskMarginEventConsumer;
    readonly IFundOrderTradeStateUIEventConsumer _tradeOrderUIEventConsumer;

/// <summary>
    /// create fund command model
    /// </summary>
    /// <param name="commandApi"></param>
    public FundCommandModel(
        IFundCommandApi commandApi,
        IFundRiskMarginUIEventConsumer fundRiskMarginEventConsumer,
        IFundOrderTradeStateUIEventConsumer tradeOrderUIEventConsumer )
    {
        _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        _fundRiskMarginEventConsumer = fundRiskMarginEventConsumer ?? throw new ArgumentNullException(nameof(fundRiskMarginEventConsumer));
        _tradeOrderUIEventConsumer = tradeOrderUIEventConsumer ?? throw new ArgumentNullException(nameof(tradeOrderUIEventConsumer));
    }

    /// <summary>
    /// create new fund 
    /// </summary>
    /// <param name="newFund"></param>
    /// <param name="onCompleted"></param>
    public async Task CreateFundAsync(FundReadModel newFund, Action onCompleted)
        => await ExecuteCommandAsync(() => _commandApi.CreateFundAsync(newFund), onCompleted);

    /// <summary>
    /// create new fund order
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <param name="setCommandId"></param>
    public async Task AddOrderToFundAsync(FundOrderReadModel fundOrder)
        => await ExecuteCommandAsync(() => _commandApi.AddOrderToFundAsync(fundOrder));

    /// <summary>
    /// delete order from fund
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task RemoveOrderFromFundAsync(FundOrderId fundOrderId)
        => await ExecuteCommandAsync(() => _commandApi.RemoveOrderFromFundAsync(fundOrderId));

    /// <summary>
    /// close fund order
    /// </summary>
    /// <param name="fundOrderId"></param>
    /// <param name="setCommandId"></param>
    public async Task CloseFundOrderAsync(FundOrderId fundOrderId)
        => await ExecuteCommandAsync(() => _commandApi.CloseFundOrderAsync(fundOrderId));

    /// <summary>
    /// add trade to fund order
    /// </summary>
    /// <param name="fundOrderTrade"></param>
    /// <param name="setCommandId"></param>
    public async Task AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade)
        => await ExecuteCommandAsync(() => _commandApi.AddTradeToFundOrderAsync(fundOrderTrade));

    /// <summary>
    /// delete trade from order
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="setCommandId"></param>
    public async Task RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId)
        => await ExecuteCommandAsync(() => _commandApi.RemoveTradeFromFundOrderAsync(fundOrderTradeId));

    /// <summary>
    /// chnage fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="setCommandId"></param>
    public async Task ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
        => await ExecuteCommandAsync(() => _commandApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState));

    /// <summary>
    /// chnage fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    /// <param name="correlationId"></param>
    public async Task ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Guid correlationId)
        => await ExecuteCommandAsync(() => _commandApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState, correlationId));

    /// <summary>
    /// create adjustment transaction
    /// </summary>
    /// <param name="adjustmentTransactionType"></param>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeStatus"></param>
    /// <param name="description"></param>
    /// <param name="amount"></param>
    /// <param name="onCompleted"></param>
    public async Task CreateAdjustmentTransactionAsync(
        FundTransactionType adjustmentTransactionType, 
        int fundId, 
        int orderId, 
        int tradeId, 
        TradeType tradeType, 
        DateOnly valueDate, 
        TradeStatus tradeStatus, 
        string description, 
        decimal amount,
        decimal balance)
        => await ExecuteCommandAsync(() => _commandApi.CreateFundTransactionAsync(FundTransactionReadModel
                    .AsAdjustmentTransaction(
                        adjustmentTransactionType: adjustmentTransactionType,
                        fundId: fundId,
                        orderId: orderId,
                        tradeId: tradeId,
                        tradeType: tradeType,
                        valueDate: valueDate,
                        tradeStatus: tradeStatus,
                        description: description,
                        amount: amount,
                        balance: balance
                )));

    /// <summary>
    /// generate fund max profit
    /// </summary>
    /// <param name="fundOrder"></param>
    /// <param name="timePeriod"></param>
    public async Task GenerateFundRiskMarginAsync(FundOrderReadModel fundOrder, TradeTimePeriodType timePeriod)
        => await ExecuteCommandAsync(() => _commandApi.GenerateFundMaxProfitAsync(fundOrder, timePeriod));

    /// <summary>
    /// start listening for fund risk margin events
    /// </summary>
    /// <param name="completeAction"></param>
    /// <param name="failAction"></param>
    public async Task StartFundRiskMarginEventConsumerAsync(
        Action<FundMaxProfitGeneratedCompleteEvent> completeAction,
        Action<FundMaxProfitGeneratedFailEvent> failAction)
        => await _fundRiskMarginEventConsumer.StartAsync(completeAction, failAction);

    /// <summary>
    /// stop listening for fund risk margin events
    /// </summary>
    /// <param name="siteId"></param>
    public async Task StopFundRiskMarginEventConsumerAsync()
        => await _fundRiskMarginEventConsumer.StopAsync();

    /// <summary>
    /// start listening for fund order trade stateui events events
    /// </summary>
    /// <param name="completeAction"></param>
    /// <param name="failAction"
    public async Task StartFundOrderTradeStateEventConsumerAsync(
        Action<FundOrderTradeStateChangedCompleteEvent> completeAction,
        Action<FundOrderTradeStateChangedFailEvent> failAction)
        => await _tradeOrderUIEventConsumer.StartAsync(completeAction, failAction);

    /// <summary>
    /// stop listening for fund order trade state ui events
    /// </summary>
    public async Task StopFundOrderTradeStateEventConsumerAsync()
        => await _tradeOrderUIEventConsumer.StopAsync();


}
