using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.ViewModels.Trade
{
    /// <summary>
    /// trade order editor view model
    /// </summary>
    public class TradeOrderEditorViewModel :BaseEditorViewModel
    {
        readonly DateTime? _valueDate;
        readonly ICollection<FuturesContractViewModel> _baseContracts;
        List<FundReadModel> _funds;
        List<FundOrderReadModel> _fundOrders;
        List<FundOrderTradeReadModel> _fundOrderTrades;
        Guid _commandId;
        Action<Guid> _setCommandId;
        ICollection<IEvent> _consumeEvents;

        FundQueryModel _fundQueryModel;
        FundCommandModel _fundCommandModel;
        ReferenceQueryModel _refQueryModel;
        FundOrderEventModel _fundOrderEventModel;

        /// <summary>
        /// trade order editor view model constructor
        /// </summary>
        /// <param name="appRoot"></param>
        /// <param name="valueDate"></param>
        /// <param name="baseContracts"></param>
        public TradeOrderEditorViewModel(IAppRoot appRoot, DateTime? valueDate, ICollection<FuturesContractViewModel> baseContracts):base(appRoot)
        {
            _valueDate = valueDate;
            _baseContracts = baseContracts;
            _fundQueryModel = appRoot.GetModel<FundQueryModel>();
            _fundCommandModel = appRoot.GetModel<FundCommandModel>();
            _refQueryModel = appRoot.GetModel<ReferenceQueryModel>();
            _fundOrderEventModel = appRoot.GetModel<FundOrderEventModel>();
            _commandId = Guid.Empty;
            _setCommandId = commandId => _commandId = commandId;
            _consumeEvents = new IEvent[]{
                new OrderAddedToFundCompleteEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new OrderAddedToFundFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new OrderRemovedFromFundCompleteEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new OrderRemovedFromFundFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new FundOrderClosedCompletedEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new FundOrderClosedFailedEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new TradeAddedToFundOrderCompleteEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new TradeAddedToFundOrderFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new TradeRemovedFromFundOrderCompleteEvent { }.SetEventSource($"{EventTopic.FundEvents}"),
                new TradeRemovedFromFundOrderFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new FundOrderTradeStateChangedCompleteEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
                new FundOrderTradeStateChangedFailEvent{ }.SetEventSource($"{EventTopic.FundEvents}"),
            };
        }

        public ICollection<FundReadModel> Funds => _funds;
        public ICollection<FundOrderReadModel> FundOrders => _fundOrders;
        public ICollection<FundOrderTradeReadModel> FundOrderTrades => _fundOrderTrades;

        public DateTime? ValueDate => _valueDate;
        public ICollection<FuturesContractViewModel> BaseContracts => _baseContracts;

        public int FundSelectedIndex { get; set; }
        public int FundOrderSelectedIndex { get; set; }
        public OrderActionType OrderActionType { get; set; }    
        public Action<string, string> ShowErrorMessage { get; set; }
        public Action OnShowButtons { get; set; }
        public Action OnFundsLoaded { get; set; }
        public Action<OrderAddedToFundCompleteEvent> OnOrderAddedToFund { get; set; }
        public Action<OrderRemovedFromFundCompleteEvent> OnOrderRemovedFromFund { get; set; }
        public Action<FundOrderTradeId, TradeState> OnFundOrderTradeStateChanged { get; set; }
        public Action<FundOrderClosedCompletedEvent> OnFundOrderClosed { get; set; }
        public Action<TradeAddedToFundOrderCompleteEvent> OnTradeAddedToFundOrder { get; set; }
        public Action<TradeRemovedFromFundOrderCompleteEvent> OnTradesRemovedFromFundOrder { get; set; }
        public Action<TradeRemovedFromFundOrderCompleteEvent> OnTradeRemovedFromFundOrder { get; set; }
        public Action<EndOfDayFundTransactionProcessedCompleteEvent> OnEndOfDayProcessed { get; set; }
        public Action<int, int> OnOptionTradeOrderPlaced { get; set; }

        public Action<TradeOrderReadModel> OnOrderSubmitted { get; set; }

        public FundReadModel GetFund(int index) => _funds.Count > 0 ? _funds[index] : default(FundReadModel);
        public int GetFundId(int index) => _funds.Count > 0 ? _funds[index].FundId : default(int);
        public FundOrderReadModel GetFundOrder(int index) => _fundOrders.Count > 0 ? _fundOrders[index] : default(FundOrderReadModel);
        public FundOrderReadModel GetFundOrder(int fundId, DateTime startDate, DateTime endDate, int index) => 
            _fundOrders.Count(e => e.FundId == fundId && e.OrderDate >= startDate && e.OrderDate <= endDate) > 0 
            ? _fundOrders.Where(e => e.FundId == fundId && e.OrderDate >= startDate && e.OrderDate <= endDate).ToList()[index]
            : default;

        public FundOrderTradeReadModel GetFundOrderTrade(int index) => _fundOrderTrades.Count > 0 ? _fundOrderTrades[index] : default(FundOrderTradeReadModel);
        public FundOrderTradeReadModel GetOpeningFundOrderTrade() 
            => _fundOrderTrades.Count > 0 ? _fundOrderTrades.Where(e => e.TradeState == TradeState.TradeToOpen).SingleOrDefault() : default(FundOrderTradeReadModel);

        public void SetCommandId(Guid commandId) => _commandId = commandId;

        public void SetSelectedFundIndex(int fundId)
        {
            this.FundSelectedIndex = 0;
            for (var index = 0; index < this.Funds.Count; index++)
            {
                if (this.Funds.ElementAt(index).FundId == fundId)
                {
                    this.FundSelectedIndex = index;
                    return;
                }
            }
        }
    
        /// <summary>
        /// add fund order to fund
        /// </summary>
        /// <param name="fundOrder"></param>
        public void AddOrderToFund(FundOrderReadModel fundOrder)
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                await model.AddOrderToFundAsync(fundOrder, _setCommandId);
            });

        /// <summary>
        /// remove order from fund
        /// </summary>
        /// <param name="fundOrderId"></param>
        public void RemoveOrderFromFund(FundOrderId fundOrderId)
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                await model.RemoveOrderFromFundAsync(fundOrderId, _setCommandId);
            });

        /// <summary>
        /// close fund order
        /// </summary>
        /// <param name="fundOrderId"></param>
        public void CloseFundOrder(FundOrderId fundOrderId)
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                await model.CloseFundOrderAsync(fundOrderId, _setCommandId);
            });

        /// <summary>
        /// add fund order trade to fund order
        /// </summary>
        /// <param name="fundOrderTrade"></param>
        public void AddTradeToFundOrder(FundOrderTradeReadModel fundOrderTrade)
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                await model.AddTradeToFundOrderAsync(fundOrderTrade, _setCommandId);
            });

        /// <summary>
        /// remove trade from fund order
        /// </summary>
        /// <param name="fundOrderTradeId"></param>
        public void RemoveTradeFromFundOrder(FundOrderTradeId fundOrderTradeId)
            => _fundCommandModel.Execute(async model => {
                 model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                 await model.RemoveTradeFromFundOrderAsync(fundOrderTradeId, _setCommandId);
             });

        /// <summary>
        /// change fund order trade state
        /// </summary>
        /// <param name="fundOrderTradeId"></param>
        /// <param name="tradeState"></param>
        public void ChangeFundOrderTradeState(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
                await model.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState, _setCommandId);
            });

        /// <summary>
        /// add trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        public void AddTradeLiveFeed(int orderId, int tradeId)
            => AppRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Add Trade Live Feed Error"));
                await model.AddTradeLiveFeedAsync(orderId, tradeId);
            });

        /// <summary>
        /// remove trade live feed
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="tradeId"></param>
        public void RemoveTradeLiveFeed(int orderId, int tradeId)
            => AppRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Remove Trade Live Feed Error"));
                await model.RemoveTradeLiveFeedAsync(orderId, tradeId);
            });

        /// <summary>
        /// remove all trade live feed for order
        /// </summary>
        /// <param name="orderId"></param>
        public void RemoveTradeLiveFeeds(int orderId)
            => AppRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Remove Trade Live Feeds Error"));
                await model.RemoveTradeLiveFeedsAsync(orderId);
            });

        /// <summary>
        /// load all fund info from storage
        /// </summary>
        public void LoadFunds()
            => _fundQueryModel.Execute(async model =>  {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Loading Funds Error"));
                await model.GetFundsAsync(async funds => await LoadFundsAsync(model, funds));
            });

        /// <summary>
        /// load all fund info from storage
        /// </summary>
        public void LoadFunds(Action onFundsLoaded)
            => _fundQueryModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Loading Funds Error"));
                await model.GetFundsAsync(async funds => await LoadFundsAsync(model, funds, onFundsLoaded));
            });

        async Task LoadFundsAsync(FundQueryModel model, FundReadModel[] funds, Action onCustomFundsLoaded = null)
        {
            if ((funds?.Length ?? 0) > 0)
            {
                var fundOrders = await model.GetFundOrdersAsync();
                var fundOrderTrades = await model.GetFundOrderTradesAsync();
                _funds = new List<FundReadModel>(funds);
                _fundOrders = new List<FundOrderReadModel>();
                _fundOrderTrades = new List<FundOrderTradeReadModel>();
                foreach (var fund in funds)
                {
                    if (fundOrders is not null)
                        foreach (var fundOrder in fundOrders.Where(e => e.FundId == fund.FundId))
                        {
                            if (fundOrderTrades is not null)
                                foreach (var trade in fundOrderTrades.Where(e => e.OrderId == fundOrder.OrderId))
                                {
                                    fundOrder.Add(trade);
                                    _fundOrderTrades.Add(trade);
                                }
                            fund.Add(fundOrder);
                            _fundOrders.Add(fundOrder);
                        }
                }
                if (onCustomFundsLoaded != null)
                    onCustomFundsLoaded.Invoke();
                else
                    OnFundsLoaded?.Invoke();
            }
        }

        /// <summary>
        /// load new trade id
        /// </summary>
        /// <param name="onCompleted"></param>
        public void LoadNewTradeId(Action<int> onCompleted)
            => _refQueryModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "New Trade Id Error"));
                await model.NewTradeIdAsync(tradeId => onCompleted?.Invoke(tradeId));
            });

        /// <summary>
        /// start fund order listener
        /// </summary>
        public void StartFundOrderListener()
          => _fundOrderEventModel.Execute(async e => {
              e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
              await e.StartFundOrderListenerAsync(_consumeEvents, HandleEventAsync);
          });

        /// <summary>
        /// stop fund order listener
        /// </summary>
        public void StopFundOrderListener()
            => _fundOrderEventModel.Execute(async e => {
                e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
                await e.StopFundOrderListenerAsync();
            });

        Task HandleEventAsync(IEvent e)
        {
            if (_commandId == Guid.Empty || _commandId != e.CommandId)
                return Task.CompletedTask;
            _commandId = Guid.Empty;
            return e switch
            {
                OrderAddedToFundCompleteEvent o => OrderAddedToFundCompleted(o),
                OrderAddedToFundFailEvent o => OrderAddedToFundFailed(o),
                OrderRemovedFromFundCompleteEvent o => OrderRemovedFromFundCompleted(o),
                OrderRemovedFromFundFailEvent o => OrderRemovedFromFundFailed(o),
                FundOrderClosedCompletedEvent o => FundOrderClosedCompleted(o),
                FundOrderClosedFailedEvent o => FundOrderClosedFailed(o),
                TradeAddedToFundOrderCompleteEvent o => TradeAddedToFundOrderCompleted(o),
                TradeAddedToFundOrderFailEvent o => TradeAddedToFundOrderFailed(o),
                TradeRemovedFromFundOrderCompleteEvent o => TradeRemovedFromFundOrderCompleted(o),
                TradeRemovedFromFundOrderFailEvent o => TradeRemovedFromFundOrderFailed(o),
                FundOrderTradeStateChangedCompleteEvent o => FundOrderTradeStateChangedCompleted(o),
                FundOrderTradeStateChangedFailEvent o => FundOrderTradeStateChangedFailed(o),
                _ => Task.CompletedTask
            };

            Task OrderAddedToFundCompleted(OrderAddedToFundCompleteEvent e)
            {
                OnOrderAddedToFund?.Invoke(e);
                return Task.CompletedTask;
            }

            Task OrderAddedToFundFailed(OrderAddedToFundFailEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "OrderAddedToFundFailed");
                return Task.CompletedTask;
            }

            Task OrderRemovedFromFundCompleted(OrderRemovedFromFundCompleteEvent e)
            {
                OnOrderRemovedFromFund?.Invoke(e);
                return Task.CompletedTask;
            }

            Task OrderRemovedFromFundFailed(OrderRemovedFromFundFailEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "OrderRemovedFromFundFailed");
                return Task.CompletedTask;
            }

            Task FundOrderClosedCompleted(FundOrderClosedCompletedEvent e)
            {
                OnFundOrderClosed?.Invoke(e);
                return Task.CompletedTask;
            }

            Task FundOrderClosedFailed(FundOrderClosedFailedEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "FundOrderClosedFailed");
                return Task.CompletedTask;
            }

            Task TradeAddedToFundOrderCompleted(TradeAddedToFundOrderCompleteEvent e)
            {
                OnTradeAddedToFundOrder?.Invoke(e);
                return Task.CompletedTask;
            }

            Task TradeAddedToFundOrderFailed(TradeAddedToFundOrderFailEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "TradeAddedToFundOrderFailed");
                return Task.CompletedTask;
            }

            Task TradeRemovedFromFundOrderCompleted(TradeRemovedFromFundOrderCompleteEvent e)
            {
                OnTradeRemovedFromFundOrder?.Invoke(e);
                return Task.CompletedTask;
            }

            Task TradeRemovedFromFundOrderFailed(TradeRemovedFromFundOrderFailEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "TradeRemovedFromFundOrderFailed");
                return Task.CompletedTask;
            }

            Task FundOrderTradeStateChangedCompleted(FundOrderTradeStateChangedCompleteEvent e)
            {
                OnFundOrderTradeStateChanged?.Invoke(e.FundOrderTradeId, e.TradeState);
                return Task.CompletedTask;
            }

            Task FundOrderTradeStateChangedFailed(FundOrderTradeStateChangedFailEvent e)
            {
                DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "FundOrderTradeStateChangedFailed");
                return Task.CompletedTask;
            }

        }

        /// <summary>
        /// start fund order trade state event consumer
        /// </summary>
        public void StartFundOrderTradeStateListener()
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Start Fund Order Trade State Event Consumer Error"));
                await model.StartFundOrderTradeStateEventConsumerAsync(e => LoadFunds(), e => { });
            });

        /// <summary>
        /// stop trade order event consumer
        /// </summary>
        public void StopFundOrderTradeStateListener()
            => _fundCommandModel.Execute(async model => {
                model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Stop Fund Order Event Consumer Error"));
                await model.StopFundOrderTradeStateEventConsumerAsync();
            });

        private void DisplayErrorMessage(int errorCode, string errorMsg, string errorCaption)
        {
            WriteStatusConsole(LogSourceType.MarketDataFeed, errorCode, errorMsg);
            this.ShowErrorMessage(errorMsg, errorCaption);
        }
    }
}
