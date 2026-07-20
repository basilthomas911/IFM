using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.Shared.Trade.Events;

namespace TomasAI.IFM.Models
{
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
        public async Task AddOrderToFundAsync(FundOrderReadModel fundOrder, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.AddOrderToFundAsync(fundOrder, setCommandId));

        /// <summary>
        /// delete order from fund
        /// </summary>
        /// <param name="fundOrderId"></param>
        /// <param name="setCommandId"></param>
        /// <returns></returns>
        public async Task RemoveOrderFromFundAsync(FundOrderId fundOrderId, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveOrderFromFundAsync(fundOrderId, setCommandId));

        /// <summary>
        /// close fund order
        /// </summary>
        /// <param name="fundOrderId"></param>
        /// <param name="setCommandId"></param>
        public async Task CloseFundOrderAsync(FundOrderId fundOrderId, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.CloseFundOrderAsync(fundOrderId, setCommandId));

        /// <summary>
        /// add trade to fund order
        /// </summary>
        /// <param name="fundOrderTrade"></param>
        /// <param name="setCommandId"></param>
        public async Task AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.AddTradeToFundOrderAsync(fundOrderTrade, setCommandId));

        /// <summary>
        /// delete trade from order
        /// </summary>
        /// <param name="fundOrderTradeId"></param>
        /// <param name="setCommandId"></param>
        public async Task RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveTradeFromFundOrderAsync(fundOrderTradeId, setCommandId));

        /// <summary>
        /// chnage fund order trade state
        /// </summary>
        /// <param name="fundOrderTradeId"></param>
        /// <param name="tradeState"></param>
        /// <param name="setCommandId"></param>
        public async Task ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState, setCommandId));

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
            DateTime valueDate, 
            TradeStatus tradeStatus, 
            string description, 
            decimal amount,
            decimal balance,
            Action<Guid> setCommandId)
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
                    ), setCommandId));

        /// <summary>
        /// generate fund max profit
        /// </summary>
        /// <param name="fundOrder"></param>
        public async Task GenerateFundRiskMarginAsync(FundOrderReadModel fundOrder)
            => await ExecuteCommandAsync(() => _commandApi.GenerateFundMaxProfitAsync(fundOrder));

        /// <summary>
        /// start listening for fund risk margin events
        /// </summary>
        /// <param name="completeAction"></param>
        /// <param name="failAction"></param>
        /// <param name="exceptionAction"></param>
        public async Task StartFundRiskMarginEventConsumerAsync(
            Action<FundMaxProfitGeneratedCompleteEvent> completeAction,
            Action<FundMaxProfitGeneratedFailEvent> failAction,
            Action<CommandExceptionEvent> exceptionAction)
            => await _fundRiskMarginEventConsumer.StartAsync(completeAction, failAction, exceptionAction);

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
}
