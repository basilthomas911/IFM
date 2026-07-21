using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.UI.Net.ViewModels.Trade;

/// <summary>
/// trade order editor view model
/// </summary>
public class TradeOrderEditorViewModel :BaseEditorViewModel
{
    readonly DateOnly? _valueDate;
    readonly ICollection<FuturesContractV2ReadModel> _baseContracts;
    List<FundReadModel> _funds = null!;
    List<FundOrderReadModel> _fundOrders = null!;
    List<FundOrderTradeReadModel> _fundOrderTrades = null!;
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
    public TradeOrderEditorViewModel(IAppRoot appRoot, DateOnly? valueDate, ICollection<FuturesContractV2ReadModel> baseContracts):base(appRoot)
    {
        _valueDate = valueDate;
        _baseContracts = baseContracts;
        _fundQueryModel = appRoot.GetModel<FundQueryModel>();
        _fundCommandModel = appRoot.GetModel<FundCommandModel>();
        _refQueryModel = appRoot.GetModel<ReferenceQueryModel>();
        _fundOrderEventModel = appRoot.GetModel<FundOrderEventModel>();
        _commandId = Guid.Empty;
        _setCommandId = commandId => _commandId = commandId;
        _consumeEvents = [
            new OrderAddedToFundCompleteEvent { },
            new OrderAddedToFundFailEvent{ },
            new OrderRemovedFromFundCompleteEvent { },
            new OrderRemovedFromFundFailEvent{ },
            new FundOrderClosedCompleteEvent { },
            new FundOrderClosedFailEvent{ },
            new TradeAddedToFundOrderCompleteEvent { },
            new TradeAddedToFundOrderFailEvent{ },
            new TradeRemovedFromFundOrderCompleteEvent { },
            new TradeRemovedFromFundOrderFailEvent { },
            new FundOrderTradeStateChangedCompleteEvent{ },
            new FundOrderTradeStateChangedFailEvent{ },
        ];
    }

    public ICollection<FundReadModel> Funds => _funds;
    public ICollection<FundOrderReadModel> FundOrders => _fundOrders;
    public ICollection<FundOrderTradeReadModel> FundOrderTrades => _fundOrderTrades;

    public DateOnly? ValueDate => _valueDate;
    public ICollection<FuturesContractV2ReadModel> BaseContracts => _baseContracts;

    public int FundSelectedIndex { get; set; }
    public int FundOrderSelectedIndex { get; set; }
    public OrderActionType OrderActionType { get; set; }    
    public Action<string, string> ShowErrorMessage { get; set; } = null!;
    public Action OnShowButtons { get; set; } = null!;
    public Action OnFundsLoaded { get; set; } = null!;
    public Action<OrderAddedToFundCompleteEvent> OnOrderAddedToFund { get; set; } = null!;
    public Action<OrderRemovedFromFundCompleteEvent> OnOrderRemovedFromFund { get; set; } = null!;
    public Action<FundOrderTradeId, TradeState> OnFundOrderTradeStateChanged { get; set; } = null!;
    public Action<FundOrderClosedCompleteEvent> OnFundOrderClosed { get; set; } = null!;
    public Action<TradeAddedToFundOrderCompleteEvent> OnTradeAddedToFundOrder { get; set; } = null!;
    public Action<TradeRemovedFromFundOrderCompleteEvent> OnTradesRemovedFromFundOrder { get; set; } = null!;
    public Action<TradeRemovedFromFundOrderCompleteEvent> OnTradeRemovedFromFundOrder { get; set; } = null!;
    public Action<EndOfDayFundTransactionProcessedCompleteEvent> OnEndOfDayProcessed { get; set; } = null!;
    public Action<int, int> OnOptionTradeOrderPlaced { get; set; } = null!;

    public Action<TradeOrderReadModel> OnOrderSubmitted { get; set; } = null!;

    public FundReadModel? GetFund(int index) 
        => _funds.Count > 0 ? _funds[index] : default;

    public int GetFundId(int index)
        => _funds.Count > 0 ? _funds[index].FundId : 0;

    public FundOrderReadModel? GetFundOrder(int index) 
        => _fundOrders.Count > 0 ? _fundOrders[index] : default;

    public FundOrderReadModel? GetFundOrder(int fundId, DateTime startDate, DateTime endDate, int index) 
        => _fundOrders.Count(e => e.FundId == fundId && e.OrderDate >= startDate && e.OrderDate <= endDate) > 0 
        ? _fundOrders.Where(e => e.FundId == fundId && e.OrderDate >= startDate && e.OrderDate <= endDate).ToList()[index]
        : default;

    public FundOrderTradeReadModel? GetFundOrderTrade(int index) 
        => _fundOrderTrades.Count > 0 ? _fundOrderTrades[index] : default;

    public FundOrderTradeReadModel? GetOpeningFundOrderTrade() 
        => _fundOrderTrades.Count > 0 ? _fundOrderTrades.Where(e => e.TradeState == TradeState.TradeToOpen).SingleOrDefault() : default;

    public void SetCommandId(Guid commandId) => _commandId = commandId;

    public void SetSelectedFundIndex(int fundId)
    {
        FundSelectedIndex = 0;
        for (var index = 0; index < Funds.Count; index++)
        {
            if (Funds.ElementAt(index).FundId == fundId)
            {
                FundSelectedIndex = index;
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
            await model.AddOrderToFundAsync(fundOrder);
        });

    /// <summary>
    /// remove order from fund
    /// </summary>
    /// <param name="fundOrderId"></param>
    public void RemoveOrderFromFund(FundOrderId fundOrderId)
        => _fundCommandModel.Execute(async model => {
            model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
            await model.RemoveOrderFromFundAsync(fundOrderId);
        });

    /// <summary>
    /// close fund order
    /// </summary>
    /// <param name="fundOrderId"></param>
    public void CloseFundOrder(FundOrderId fundOrderId)
        => _fundCommandModel.Execute(async model => {
            model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
            await model.CloseFundOrderAsync(fundOrderId);
        });

    /// <summary>
    /// add fund order trade to fund order
    /// </summary>
    /// <param name="fundOrderTrade"></param>
    public void AddTradeToFundOrder(FundOrderTradeReadModel fundOrderTrade)
        => _fundCommandModel.Execute(async model => {
            model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
            await model.AddTradeToFundOrderAsync(fundOrderTrade);
        });

    /// <summary>
    /// remove trade from fund order
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    public void RemoveTradeFromFundOrder(FundOrderTradeId fundOrderTradeId)
        => _fundCommandModel.Execute(async model => {
             model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
             await model.RemoveTradeFromFundOrderAsync(fundOrderTradeId);
         });

    /// <summary>
    /// change fund order trade state
    /// </summary>
    /// <param name="fundOrderTradeId"></param>
    /// <param name="tradeState"></param>
    public void ChangeFundOrderTradeState(FundOrderTradeId fundOrderTradeId, TradeState tradeState)
        => _fundCommandModel.Execute(async model => {
            model.OnError((errorCode, errorMessage) => OnError?.Invoke(errorCode, errorMessage));
            await model.ChangeFundOrderTradeStateAsync(fundOrderTradeId, tradeState);
        });

    /// <summary>
    /// add trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public void AddTradeLiveFeed(int orderId, int tradeId)
        => AppRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Add Trade Live Feed Error"));
            await model.AddTradeLiveFeedAsync(orderId, tradeId, _valueDate!.Value);
        });

    /// <summary>
    /// remove trade live feed
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public void RemoveTradeLiveFeed(int orderId, int tradeId)
        => AppRoot.GetModel<MarketDataFeedCommandModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => DisplayErrorMessage(errorCode, errorMsg, "Remove Trade Live Feed Error"));
            await model.RemoveTradeLiveFeedAsync(orderId, tradeId, _valueDate!.Value);
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

    async Task LoadFundsAsync(FundQueryModel model, FundReadModel[] funds, Action onCustomFundsLoaded = null!)
    {
        if (funds.Length > 0)
        {
            var fundOrders = await model.GetFundOrdersAsync();
            var fundOrderTrades = await model.GetFundOrderTradesAsync();
            _funds = [.. funds];
            _fundOrders = [];
            _fundOrderTrades = [];
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
          await e.StartFundOrderListenerAsync(HandleEventAsync);
      });

    /// <summary>
    /// stop fund order listener
    /// </summary>
    public void StopFundOrderListener()
        => _fundOrderEventModel.Execute(async e => {
            e.OnError((errorCode, errorMsg) => OnError(errorCode, errorMsg));
            await e.StopFundOrderListenerAsync();
        });

    ValueTask HandleEventAsync(IEvent e)
    {
        if (_commandId == Guid.Empty || _commandId != e.CommandId)
            return ValueTask.CompletedTask;
        _commandId = Guid.Empty;
        return e switch
        {
            OrderAddedToFundCompleteEvent o => OrderAddedToFundCompleted(o),
            OrderAddedToFundFailEvent o => OrderAddedToFundFailed(o),
            OrderRemovedFromFundCompleteEvent o => OrderRemovedFromFundCompleted(o),
            OrderRemovedFromFundFailEvent o => OrderRemovedFromFundFailed(o),
            FundOrderClosedCompleteEvent o => FundOrderClosedCompleted(o),
            FundOrderClosedFailEvent o => FundOrderClosedFailed(o),
            TradeAddedToFundOrderCompleteEvent o => TradeAddedToFundOrderCompleted(o),
            TradeAddedToFundOrderFailEvent o => TradeAddedToFundOrderFailed(o),
            TradeRemovedFromFundOrderCompleteEvent o => TradeRemovedFromFundOrderCompleted(o),
            TradeRemovedFromFundOrderFailEvent o => TradeRemovedFromFundOrderFailed(o),
            FundOrderTradeStateChangedCompleteEvent o => FundOrderTradeStateChangedCompleted(o),
            FundOrderTradeStateChangedFailEvent o => FundOrderTradeStateChangedFailed(o),
            _ => ValueTask.CompletedTask
        };

        ValueTask OrderAddedToFundCompleted(OrderAddedToFundCompleteEvent e)
        {
            OnOrderAddedToFund?.Invoke(e);
            return ValueTask.CompletedTask;
        }

        ValueTask OrderAddedToFundFailed(OrderAddedToFundFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "OrderAddedToFundFailed");
            return ValueTask.CompletedTask;
        }

        ValueTask OrderRemovedFromFundCompleted(OrderRemovedFromFundCompleteEvent e)
        {
            OnOrderRemovedFromFund?.Invoke(e);
            return ValueTask.CompletedTask;
        }

        ValueTask OrderRemovedFromFundFailed(OrderRemovedFromFundFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "OrderRemovedFromFundFailed");
            return ValueTask.CompletedTask;
        }

        ValueTask FundOrderClosedCompleted(FundOrderClosedCompleteEvent e)
        {
            OnFundOrderClosed?.Invoke(e);
            return ValueTask.CompletedTask;
        }

        ValueTask FundOrderClosedFailed(FundOrderClosedFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "FundOrderClosedFailed");
            return ValueTask.CompletedTask;
        }

        ValueTask TradeAddedToFundOrderCompleted(TradeAddedToFundOrderCompleteEvent e)
        {
            OnTradeAddedToFundOrder?.Invoke(e);
            return ValueTask.CompletedTask;
        }

        ValueTask TradeAddedToFundOrderFailed(TradeAddedToFundOrderFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "TradeAddedToFundOrderFailed");
            return ValueTask.CompletedTask;
        }

        ValueTask TradeRemovedFromFundOrderCompleted(TradeRemovedFromFundOrderCompleteEvent e)
        {
            OnTradeRemovedFromFundOrder?.Invoke(e);
            return ValueTask.CompletedTask;
        }

        ValueTask TradeRemovedFromFundOrderFailed(TradeRemovedFromFundOrderFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "TradeRemovedFromFundOrderFailed");
            return ValueTask.CompletedTask;
        }

        ValueTask FundOrderTradeStateChangedCompleted(FundOrderTradeStateChangedCompleteEvent e)
        {
            OnFundOrderTradeStateChanged?.Invoke(e.FundOrderTradeId, e.TradeState);
            return ValueTask.CompletedTask;
        }

        ValueTask FundOrderTradeStateChangedFailed(FundOrderTradeStateChangedFailEvent e)
        {
            DisplayErrorMessage(e.ErrorCode, e.ErrorMessage, "FundOrderTradeStateChangedFailed");
            return ValueTask.CompletedTask;
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
