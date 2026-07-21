using System.Globalization;
using QLNet;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

public partial class IronCondorTradeOrderView : UserControl, IFormControl, ITradeOrderControl
{
    IronCondorTradeOrderReadModel _viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="IronCondorTradeOrderView"/> class with the specified parent control
    /// and view model.
    /// </summary>
    /// <remarks>This constructor sets up the necessary event handlers and actions to synchronize the view
    /// with the view model. It ensures that UI updates and interactions are properly handled, such as displaying error
    /// messages, updating trade details, and managing order actions.</remarks>
    /// <param name="parentControl">The parent control that hosts this view. This is used to interact with the parent form for certain actions.</param>
    /// <param name="viewModel">The view model that provides data and commands for the view. This view subscribes to various events and actions
    /// from the view model to update the UI accordingly.</param>
    public IronCondorTradeOrderView(Control parentControl, IronCondorTradeOrderReadModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.ShowErrorMessage = (errorMsg, caption) => this.ShowErrorMessage(errorMsg, caption);
        _viewModel.DrawView = (drawAction) => this.Draw(drawAction);
        _viewModel.ShowIronCondorTrade = () => parentControl.Post(() =>
        {
            LoadTradeActions();
            LoadOptionTypes();
            LoadOrderTypes();
            SetReadOnlyControls();
            ShowIronCondorTradeDetails();
            ShowIronCondorTradePositions();
        });
        _viewModel.ShowTradeLimits = (tradeLimit, fundBalance) => this.Invoke(() => ShowTradeLimits((tradeLimit, fundBalance)));
        _viewModel.ShowTradeTypeLimits = (putCreditSpreadTypeLimit, callCreditSpreadTypeLimit) => this.Invoke(() =>
        {
            ShowPutCreditSpreadTradeTypeLimit(putCreditSpreadTypeLimit);
            ShowCallCreditSpreadTradeTypeLimit(callCreditSpreadTypeLimit);
        });
        _viewModel.ShowLiveFeedValues = (futuresEodData) => this.Invoke(() => ShowLiveFeedValues(futuresEodData));
        _viewModel.ShowStrikePrices = (strikePriceRange, selectedIndex) => this.Invoke(() =>
        {
            var ddlStrikePrices = new ComboBox[] { ddlLeg1StrikePrice, ddlLeg2StrikePrice, ddlLeg3StrikePrice, ddlLeg4StrikePrice };
            foreach (var e in ddlStrikePrices)
            {
                e.Items.Clear();
                e.Items.AddRange(strikePriceRange);
            }
        });
        _viewModel.ShowAssetPrice = assetPrice => this.Post(() => ShowAssetPrice(assetPrice));
        _viewModel.OrderActionTypeChanged = orderActionType =>
        {
            _viewModel.SetOrderAction(orderActionType);
            this.Draw(() => ((TradeOrderEditorForm)parentControl).SetOrderAction(orderActionType));
        };
       
        _viewModel.FundMaxProfitChanged = e => this.Invoke(() => { 
            txtRiskProfit.Text = $"{e.FundMaxProfit.FundMaxProfit:C}";
            _viewModel.StopFundRiskMarginEventConsumer();
        });
        _viewModel.FundMaxProfitFailed = e => this.Invoke(() => {
            this.ShowErrorMessage(e.ErrorMessage, "Setting Risk Margin Failed");
            _viewModel.StopFundRiskMarginEventConsumer();
        });
        _viewModel.FundMaxProfitException = e => this.Invoke(() => {
            this.ShowErrorMessage(e.ErrorMessage, "Setting Risk Margin Exception");
            _viewModel.StopFundRiskMarginEventConsumer();
        });
    }

    void IronCondorTradeOrderControl_Load(object sender, EventArgs e)
    {
        _viewModel.LoadIronCondorTradeOrders();
    }

    public DateOnly MaturityDate => DateOnly.FromDateTime(dtmLeg1LastTradeDate.Value);

    public void Open()
    {
    }

    public void Close()
    {
    }

    void IFormControl.Resize(Control parentControl)
    {
        throw new NotImplementedException();
    }

    public void RemoveTrade(int fundId, int orderId, int tradeId) => _viewModel.RemoveTradeFromFundOrder( new FundOrderTradeId(fundId, orderId, tradeId));

    public void SubmitOrder(DateOnly tradeDate, OrderActionType orderActionType, TradeOrderConfirmationViewModel tradeOrderConfirmation, Action<Guid> setCommmandId)
    {
        _viewModel.SetTradeStatus(orderActionType);
        _viewModel.SetTradeDate(tradeDate);
        _viewModel.SetMaturityDate(this.MaturityDate);
        _viewModel.SetDaysToExpiry();
        _viewModel.IronCondorTrade.SetTradeLimit(
            maxLoss: -1m * _viewModel.FundBalance * 0.02m,
            minProfitTarget: (0.50m * _viewModel.IronCondorTrade.TradeLimit!.MaxProfit) + (2 * _viewModel.TradeCommission),
            dailyProfitTarget: (_viewModel.IronCondorTrade.TradeLimit.MaxProfit / _viewModel.DaysToExpiry) + (2 * _viewModel.TradeCommission));
        var contractId = _viewModel.BaseContract.ContractId;
        var optionLeg1StrikePrice = $"{_viewModel.GetStrikePrice(_viewModel.OptionLeg1Action, OptionType.Put):F0}";
        var optionLeg2StrikePrice = $"{_viewModel.GetStrikePrice(_viewModel.OptionLeg2Action, OptionType.Put):F0}";
        var optionLeg3StrikePrice = $"{_viewModel.GetStrikePrice(_viewModel.OptionLeg3Action, OptionType.Call):F0}";
        var optionLeg4StrikePrice = $"{_viewModel.GetStrikePrice(_viewModel.OptionLeg4Action, OptionType.Call):F0}";
        var orderAction = _viewModel.GetOrderAction();
        var orderType = (OrderType)Enum.Parse(typeof(OrderType), $"{ddlOrderType.SelectedItem}");
        var tradeFillType = TradeFillType.Manual;
        var orderAmount = _viewModel.OrderAmount;
        var totalAmount = _viewModel.TotalAmount;
        _viewModel.GetIntraDayPnl(intraDayPnl =>
        {
            var tradeOrder = new TradeOrderReadModel
            (
                fundId: _viewModel.FundId,
                orderId: _viewModel.IronCondorTrade.OrderId,
                tradeId: _viewModel.IronCondorTrade.TradeId,
                valueDate: _viewModel.ValueDate,
                tradeType: _viewModel.IronCondorTrade.TradeType,
                tradeSubType: TradeSubType.Primary,
                tradeDate: _viewModel.IronCondorTrade.TradeDate,
                maturityDate: _viewModel.IronCondorTrade.MaturityDate,
                tradeOrderState: Shared.TradeOrder.TradeOrderState.OrderPlaced,
                underlyingContractId: _viewModel.IronCondorTrade.UnderlyingContractId,
                underlyingAssetType: _viewModel.IronCondorTrade.UnderlyingAssetType,
                orderDescription: $"{contractId} @ P{optionLeg1StrikePrice}:{optionLeg2StrikePrice} x C{optionLeg3StrikePrice}:{optionLeg4StrikePrice}",
                orderAction: orderAction,
                orderActionType: orderActionType,
                orderQuantity: _viewModel.OrderQuantity,
                orderFilled: _viewModel.OrderQuantity,
                orderType: orderType,
                orderPrice: _viewModel.OrderPrice,
                orderAmount: orderAmount,
                commission: _viewModel.TradeCommission,
                totalAmount: totalAmount,
                tradePnl: intraDayPnl,
                tradeFillType: tradeFillType,
                createdOn: DateTime.Now,
                createdBy: $"{Environment.UserDomainName}\\{Environment.UserName}",
                updatedOn: DateTime.Now,
                updatedBy: $"{Environment.UserDomainName}\\{Environment.UserName}"
            );
            tradeOrderConfirmation.TradeOrder = tradeOrder;
            if (tradeOrderConfirmation.ShowOrderConfirmation())
                _viewModel.SubmitOrder(tradeOrder, setCommmandId);

        });
    }

    public void LiveFeed(bool enabled)
    {
        ClearPrices();
        if (enabled)
            _viewModel.TurnLiveFeedOn();
        else
            _viewModel.TurnLiveFeedOff();   
    }

    public void SetNearestStrikePrices()
    {
        SetStrikePriceWidth(ddlLeg1StrikePrice, _viewModel.NearestPutStrike);
        SetStrikePriceWidth(ddlLeg3StrikePrice, _viewModel.NearestCallStrike);
        txtLeg1ExpectedOTMProbability.Text = $"{_viewModel.OTMPutProbability:P2}";
        txtLeg3ExpectedOTMProbability.Text = $"{_viewModel.OTMCallProbability:P2}";

    }

    public void ShowAssetPrice(decimal assetPrice) => txtAssetPrice.Text = $"{assetPrice:C}";

    public void ShowIronCondorTradeDetails()
    {
        dtmLeg1LastTradeDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
        dtmLeg2LastTradeDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
        dtmLeg3LastTradeDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
        dtmLeg4LastTradeDate.Value = _viewModel.MaturityDate.ToDateTime(TimeOnly.MinValue);
        if (txtLeg1BidPrice.ReadOnly)
        {
            ddlLeg1StrikePrice.Items.Clear();
            ddlLeg1StrikePrice.Items.Add($"{_viewModel.GetStrikePrice(_viewModel.OptionLeg1Action, OptionType.Put):F0}");
            ddlLeg1StrikePrice.SelectedIndex = 0;
        }
        else
            ddlLeg1StrikePrice.SetSelectedIndex((int)_viewModel.GetStrikePrice(_viewModel.OptionLeg1Action, OptionType.Put));
        if (txtLeg2BidPrice.ReadOnly)
        {
            ddlLeg2StrikePrice.Items.Clear();
            ddlLeg2StrikePrice.Items.Add($"{_viewModel.GetStrikePrice(_viewModel.OptionLeg2Action, OptionType.Put):F0}");
            ddlLeg2StrikePrice.SelectedIndex = 0;
        }
        else
            ddlLeg2StrikePrice.SetSelectedIndex((int)_viewModel.GetStrikePrice(_viewModel.OptionLeg2Action, OptionType.Put));
        if (txtLeg3BidPrice.ReadOnly)
        {
            ddlLeg3StrikePrice.Items.Clear();
            ddlLeg3StrikePrice.Items.Add($"{_viewModel.GetStrikePrice(_viewModel.OptionLeg3Action, OptionType.Call):F0}");
            ddlLeg3StrikePrice.SelectedIndex = 0;
        }
        else
            ddlLeg3StrikePrice.SetSelectedIndex((int)_viewModel.GetStrikePrice(_viewModel.OptionLeg3Action, OptionType.Call));

        if (txtLeg4BidPrice.ReadOnly)
        {
            ddlLeg4StrikePrice.Items.Clear();
            ddlLeg4StrikePrice.Items.Add($"{_viewModel.GetStrikePrice(_viewModel.OptionLeg4Action, OptionType.Call):F0}");
            ddlLeg4StrikePrice.SelectedIndex = 0;
        }
        else
            ddlLeg4StrikePrice.SetSelectedIndex((int)_viewModel.GetStrikePrice(_viewModel.OptionLeg4Action, OptionType.Call));
        nudQuantity.Value = _viewModel.OrderQuantity;
    }

    public void ShowIronCondorTradePositions()
    {
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put);
        var pcs = _viewModel.GetTradePosition(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus);
        txtLeg1BidPrice.Text = $"{(optionLegData?.BidPrice ?? 0m):C}";
        txtLeg1AskPrice.Text = $"{(optionLegData?.AskPrice ?? 0m):C}";
        txtLeg1NetSpread.Text = $"{(pcs?.NetSpread ?? 0m):C}";
        txtLeg1TradeValue.Text = $"{(pcs?.TradeValue ?? 0m):C}";
        txtLeg1ActualOTMProbability.Text = $"{(pcs?.OTMProbability ?? 0.0):P2}";
        optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put);
        txtLeg2BidPrice.Text = $"{(optionLegData?.BidPrice ?? 0m):C}";
        txtLeg2AskPrice.Text = $"{(optionLegData?.AskPrice ?? 0m):C}";
        optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call);
        var ccs = _viewModel.GetTradePosition(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus);
        txtLeg3BidPrice.Text = $"{(optionLegData?.BidPrice ?? 0m):C}";
        txtLeg3AskPrice.Text = $"{(optionLegData?.AskPrice ?? 0m):C}";
        txtLeg3NetSpread.Text = $"{(ccs?.NetSpread ?? 0m):C}";
        txtLeg3TradeValue.Text = $"{(ccs?.TradeValue ?? 0m):C}";
        txtLeg3ActualOTMProbability.Text = $"{(ccs?.OTMProbability ?? 0.0):P2}";
        optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call);
        txtLeg4BidPrice.Text = $"{(optionLegData?.BidPrice ?? 0m):C}";
        txtLeg4AskPrice.Text = $"{(optionLegData?.AskPrice ?? 0m):C}";
    }

    public void ShowTradeLimits((TradeLimitReadModel TradeLimit, decimal FundBalance) e)
    {
        if (e.TradeLimit is null) return;
        txtFundBalance.Text = $"{e.FundBalance:C}";
        txtRiskMargin.Text = $"{e.TradeLimit.RiskMargin:C}";
        txtMaxProfit.Text = $"{e.TradeLimit.MaxProfit:C}";
        txtMaxReturn.Text = $"{e.TradeLimit.MaxReturn:P2}";
        txtMaxLossLimit.Text = $"{e.TradeLimit.MaxLossLimit:F4}";
        txtMaxProfitLimit.Text = $"{e.TradeLimit.MaxProfitLimit:F4}";
        txtMinProfitTarget.Text = $"{e.TradeLimit.MinProfitTarget:C}";
    }

    public void ShowPutCreditSpreadTradeTypeLimit(TradeTypeLimitReadModel e)
    {
        if (e is null) return;
        txtLeg1MaxLossLimit.Text = $"{e.MaxLossLimit:F4}";
        txtLeg1MinProfitLimit.Text = $"{e.MinProfitLimit:F4}";
    }

    public void ShowCallCreditSpreadTradeTypeLimit(TradeTypeLimitReadModel e)
    {
        if (e is null) return;
        txtLeg3MaxLossLimit.Text = $"{e.MaxLossLimit:F4}";
        txtLeg3MinProfitLimit.Text = $"{e.MinProfitLimit:F4}";
    }

    void SetReadOnlyControls()
    {
        var readOnly = _viewModel.FundOrderTrade.TradeState != TradeState.NewTrade;
        var controls = new Control[] { 
            txtLeg1BidPrice, txtLeg1AskPrice, txtLeg1ActualOTMProbability,txtLeg1MaxLossLimit, txtLeg1ExpectedOTMProbability, txtLeg1MinProfitLimit, txtLeg1NetSpread, txtLeg1TradeValue,
            txtLeg2BidPrice, txtLeg2AskPrice, 
            txtLeg3BidPrice, txtLeg3AskPrice, txtLeg3ActualOTMProbability, txtLeg3MaxLossLimit, txtLeg3ExpectedOTMProbability, txtLeg3MinProfitLimit, txtLeg3NetSpread, txtLeg3TradeValue,
            txtLeg4BidPrice, txtLeg4AskPrice,
            txtFundBalance, txtRiskMargin, txtMaxLossLimit, txtMaxProfit, txtMaxProfitLimit, txtMaxReturn, txtMinProfitTarget, txtAssetPrice};
        EnableControls(readOnly, controls);
    }

    void ShowLiveFeedValues(FuturesEodDataV2ReadModel futuresEodData)
    {
        ShowTradeValues();
        txtAssetPrice.Text = $"{futuresEodData.ClosePrice:C}";

        // show put spread market data...
        txtLeg1BidPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put)?.BidPrice ?? 0m):C}";
        txtLeg1AskPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put)?.AskPrice ?? 0m):C}";
        txtLeg2BidPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put)?.BidPrice ?? 0m):C}";
        txtLeg2AskPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put)?.AskPrice ?? 0m):C}";

        // show call credit spread market data...
        txtLeg3BidPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call)?.BidPrice ?? 0m):C}";
        txtLeg3AskPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call)?.AskPrice ?? 0m):C}";
        txtLeg4BidPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call)?.BidPrice ?? 0m):C}";
        txtLeg4AskPrice.Text = $"{(_viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call)?.AskPrice ?? 0m):C}";
    }

    void ShowTradeValues()
    {
        // show net spread values...
        var spreadTrade = _viewModel.IronCondorTrade;
        var pcs = _viewModel.GetTradePosition(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus);
        if (pcs is null) return;
        var ccs = _viewModel.GetTradePosition(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus);
        if (ccs is null) return;
        txtLeg1NetSpread.Text = $"{(pcs?.NetSpread ?? 0m):C}";
        txtLeg1ActualOTMProbability.Text = $"{pcs?.OTMProbability:P2}";
        txtLeg3NetSpread.Text = $"{(ccs?.NetSpread ?? 0m):C}";
        txtLeg3ActualOTMProbability.Text = $"{ccs?.OTMProbability:P2}";

        // show trade values...
        txtLeg1TradeValue.Text = $"{(pcs?.TradeValue ?? 0m):C}";
        txtLeg3TradeValue.Text = $"{(ccs?.TradeValue ?? 0m):C}";

        // show trade type limits...
        txtLeg1MaxLossLimit.Text = $"{(spreadTrade.TradeTypeLimits!.Get(_viewModel.PutSpreadTradeType)?.MaxLossLimit ?? 0.0m):F4}";
        txtLeg1MinProfitLimit.Text = $"{(spreadTrade.TradeTypeLimits!.Get(_viewModel.PutSpreadTradeType)?.MinProfitLimit ?? 0.0m):F4}";
        txtLeg3MaxLossLimit.Text = $"{(spreadTrade.TradeTypeLimits!.Get(_viewModel.CallSpreadTradeType)?.MaxLossLimit ?? 0.0m):F4}";
        txtLeg3MinProfitLimit.Text = $"{(spreadTrade.TradeTypeLimits!.Get(_viewModel.CallSpreadTradeType)?.MinProfitLimit ?? 0.0m):F4}";

        // show trade limits...
        txtMaxProfit.Text = $"{spreadTrade.TradeLimit!.MaxProfit:C}";
        txtMaxReturn.Text = $"{spreadTrade.TradeLimit.MaxReturn:P2}";
        txtMaxLossLimit.Text = $"{spreadTrade.TradeLimit.MaxLossLimit:F4}";
        txtMaxProfitLimit.Text = $"{spreadTrade.TradeLimit.MinProfitLimit:F4}";
        txtMinProfitTarget.Text = $"{spreadTrade.TradeLimit.MinProfitTarget:C}";

        // populate order price...
        var netSpreadPrice = (Convert.ToDouble(pcs?.NetSpread ?? 0m) + Convert.ToDouble(ccs?.NetSpread ?? 0m));
        PopulateOrderPrice(netSpreadPrice);
    }

    void PopulateOrderPrice(double netSpreadPrice)
    {
        ddlPrice.Items.Clear();
        var orderPrices = new List<double>();
        if ((netSpreadPrice * 2) >= 5.0)
        {
            for (var netPrice = 0.05; netPrice < 5.0; netPrice += 0.05)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
            for (var netPrice = 5.25; netPrice < (netSpreadPrice * 2); netPrice += 0.25)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
        }
        else if (netSpreadPrice >= 0.0)
        {
            for (var netPrice = 0.05; netPrice < (netSpreadPrice * 2); netPrice += 0.05)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
        }
        else if ((netSpreadPrice * 2) <= -5.0)
        {
            for (var netPrice = -0.05; netPrice > -5.0; netPrice += -0.05)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
            for (var netPrice = -5.25; netPrice > (netSpreadPrice * 2); netPrice += -0.25)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
        }
        else if (netSpreadPrice < 0.0)
        {
            for (var netPrice = -0.05; netPrice > (netSpreadPrice * 2); netPrice += -0.05)
                orderPrices.Add(Convert.ToDouble($"{netPrice:F2}"));
        }
        var selectedIndex = 0;
        for (var index = 0; index < orderPrices.Count; index++)
        {
            if (netSpreadPrice >= 0.0)
            {
                if (orderPrices[index] > netSpreadPrice)
                {
                    selectedIndex = index > 0 ? index - 1 : 0;
                    break;
                }
            }
            else if (orderPrices[index] < netSpreadPrice)
            {
                selectedIndex = index > 0 ? index - 1 : 0;
                break;
            }
        }
        orderPrices.ForEach(e => ddlPrice.Items.Add(_viewModel.TradeType == TradeType.ShortIronCondor ? $"-{e:N2}" : $"{e:N2}"));
        if (orderPrices.Count > 0)
            ddlPrice.SelectedIndex = selectedIndex;
        else
        {
            ddlPrice.Items.Add(netSpreadPrice);
            ddlPrice.SelectedIndex = 0;
        }
    }


    void LoadTradeActions()
    {
        // put spread leg 1 option...
        ddlLeg1Action.Items.Clear();
        ddlLeg1Action.Items.Add($"{_viewModel.OptionLeg1Action}");
        ddlLeg1Action.SelectedIndex = 0;
        ddlLeg1Action.Enabled = true;

        // put spread leg 2 option...
        ddlLeg2Action.Items.Clear();
        ddlLeg2Action.Items.Add($"{_viewModel.OptionLeg2Action}");
        ddlLeg2Action.SelectedIndex = 0;
        ddlLeg2Action.Enabled = true;

        // call spread leg 3 option...
        ddlLeg3Action.Items.Clear();
        ddlLeg3Action.Items.Add($"{_viewModel.OptionLeg3Action}");
        ddlLeg3Action.SelectedIndex = 0;
        ddlLeg3Action.Enabled = true;

        // call spread leg long option...
        ddlLeg4Action.Items.Clear();
        ddlLeg4Action.Items.Add($"{_viewModel.OptionLeg4Action}");
        ddlLeg4Action.SelectedIndex = 0;
        ddlLeg4Action.Enabled = true;
    }

    void LoadOptionTypes()
    {
        // put spread leg 1 option...
        ddlLeg1OptionType.Items.Clear();
        ddlLeg1OptionType.Items.Add($"{OptionType.Put}");
        ddlLeg1OptionType.SelectedIndex = 0;
        ddlLeg1OptionType.Enabled = true;

        // put spread leg 2 option...
        ddlLeg2OptionType.Items.Clear();
        ddlLeg2OptionType.Items.Add($"{OptionType.Put}");
        ddlLeg2OptionType.SelectedIndex = 0;
        ddlLeg2OptionType.Enabled = true;

        // call spread leg 3 option...
        ddlLeg3OptionType.Items.Clear();
        ddlLeg3OptionType.Items.Add($"{OptionType.Call}");
        ddlLeg3OptionType.SelectedIndex = 0;
        ddlLeg3OptionType.Enabled = true;

        // call spread leg 4 option...
        ddlLeg4OptionType.Items.Clear();
        ddlLeg4OptionType.Items.Add($"{OptionType.Call}");
        ddlLeg4OptionType.SelectedIndex = 0;
        ddlLeg4OptionType.Enabled = true;
    }

    void LoadOrderTypes()
    {
        ddlOrderType.Items.Clear();
        ddlOrderType.Items.Add($"{OrderType.Limit}");
        ddlOrderType.Items.Add($"{OrderType.Market}");
        ddlOrderType.Items.Add($"{OrderType.CloseAlgo}");
        ddlOrderType.SelectedIndex = 0;
    }

    void dtmLeg1LastTradeDate_ValueChanged(object sender, EventArgs e)
    {
        _viewModel.UpdateDaysToExpiryAction(DateOnly.FromDateTime(dtmLeg1LastTradeDate.Value));
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var dtpLastTradeDates = new List<DateTimePicker> { dtmLeg2LastTradeDate, dtmLeg3LastTradeDate, dtmLeg4LastTradeDate };
        dtpLastTradeDates.ForEach(dtp => dtp.Value = dtmLeg1LastTradeDate.Value);
        var contractId = $"{_viewModel.BaseContract.Symbol}{dtmLeg1LastTradeDate.Value:yyyyMMdd}";
        for (var index = 0; index < _viewModel.OptionLegs.Length; index++)
        {
            var optionLeg = _viewModel.OptionLegs[index];
            var optionType = optionLeg.OptionLegType == OptionType.Put ? "P" : "C";
            optionLeg = optionLeg with { ContractId = $"{contractId}{optionType}{optionLeg.StrikePrice:F0}" };
            _viewModel.OptionLegs[index] = optionLeg;
        }
    }

    void ddlLeg1StrikePrice_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var strikePrice = Convert.ToInt32(ddlLeg1StrikePrice.SelectedItem);
        ddlLeg2StrikePrice.SetSelectedIndex(_viewModel.SetPutSpreadStrike(strikePrice));
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg1Action, OptionType.Put);
        optionLeg = optionLeg with { StrikePrice = strikePrice };
        var contractId = $"{_viewModel.BaseContract.Symbol}{dtmLeg1LastTradeDate.Value:yyyyMMdd}";
        var optionType = optionLeg.OptionLegType == OptionType.Put ? "P" : "C";
        optionLeg = optionLeg with { ContractId = $"{contractId}{optionType}{optionLeg.StrikePrice:F0}" };
        _viewModel.SetOptionLeg(_viewModel.ShortOptionLegAction, OptionType.Put, optionLeg);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put)
            .SetOptionLeg(optionLeg);
        _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put, optionLegData);
    }

    void ddlLeg2StrikePrice_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var strikePrice = Convert.ToInt32(ddlLeg2StrikePrice.SelectedItem);
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg2Action, OptionType.Put);
        optionLeg = optionLeg with { StrikePrice = strikePrice };
        var contractId = $"{_viewModel.BaseContract.Symbol}{dtmLeg2LastTradeDate.Value:yyyyMMdd}";
        var optionType = optionLeg.OptionLegType == OptionType.Put ? "P" : "C";
        optionLeg = optionLeg with { ContractId = $"{contractId}{optionType}{optionLeg.StrikePrice:F0}" };
        _viewModel.SetOptionLeg(_viewModel.LongOptionLegAction, OptionType.Put, optionLeg);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put)
            .SetOptionLeg(optionLeg);
        _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put, optionLegData);
    }

    void ddlLeg3StrikePrice_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var strikePrice = Convert.ToInt32(ddlLeg3StrikePrice.SelectedItem);
        ddlLeg4StrikePrice.SetSelectedIndex(_viewModel.SetCallSpreadStrike(strikePrice));
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg3Action, OptionType.Call);
        optionLeg = optionLeg with { StrikePrice = strikePrice };
        var contractId = $"{_viewModel.BaseContract.Symbol}{dtmLeg3LastTradeDate.Value:yyyyMMdd}";
        var optionType = optionLeg.OptionLegType == OptionType.Put ? "P" : "C";
        optionLeg = optionLeg with { ContractId = $"{contractId}{optionType}{optionLeg.StrikePrice:F0}" };
        _viewModel.SetOptionLeg(_viewModel.ShortOptionLegAction, OptionType.Call, optionLeg);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call)
            .SetOptionLeg(optionLeg);
        _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call, optionLegData);
    }

    void ddlLeg4StrikePrice_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var strikePrice = Convert.ToInt32(ddlLeg4StrikePrice.SelectedItem);
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg4Action, OptionType.Call);
        optionLeg = optionLeg with { StrikePrice = strikePrice };
        var contractId = $"{_viewModel.BaseContract.Symbol}{dtmLeg4LastTradeDate.Value:yyyyMMdd}";
        var optionType = optionLeg.OptionLegType == OptionType.Put ? "P" : "C";
        optionLeg = optionLeg with { ContractId = $"{contractId}{optionType}{optionLeg.StrikePrice:F0}" };
        _viewModel.SetOptionLeg(_viewModel.LongOptionLegAction, OptionType.Call, optionLeg);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call)
            .SetOptionLeg(optionLeg);
        _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call, optionLegData);
    }

    void SetStrikePriceWidth(ComboBox ddlStrikePrice, int strikePrice)
    {
        for (var index = 0; index < ddlStrikePrice.Items.Count; index++)
            if (Convert.ToInt32(ddlStrikePrice.Items[index]) == strikePrice)
            {
                ddlStrikePrice.SelectedIndex = index;
                break;
            }
    }

    void ddlLeg1OptionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg1Action, OptionType.Put);
        optionLeg = optionLeg with { OptionLegType = ParseForOptionType(ddlLeg1OptionType) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg1Action, OptionType.Put, optionLeg);
    }

    void ddlLeg2OptionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg2Action, OptionType.Put);
        optionLeg = optionLeg with { OptionLegType = ParseForOptionType(ddlLeg2OptionType) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg2Action, OptionType.Put, optionLeg);
    }

    void ddlLeg3OptionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg3Action, OptionType.Call);
        optionLeg = optionLeg with { OptionLegType = ParseForOptionType(ddlLeg3OptionType) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg3Action, OptionType.Call, optionLeg);
    }

    void ddlLeg4OptionType_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg4Action, OptionType.Call);
        optionLeg = optionLeg with { OptionLegType = ParseForOptionType(ddlLeg4OptionType) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg4Action, OptionType.Call, optionLeg);
    }

    void ddlLeg1Action_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg1Action, OptionType.Put);
        optionLeg = optionLeg with { OptionLegAction = ParseForOptionLegAction(ddlLeg1Action)};
        _viewModel.SetOptionLeg(_viewModel.OptionLeg1Action, OptionType.Put, optionLeg);
    }

    void ddlLeg2Action_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg2Action, OptionType.Put);
        optionLeg = optionLeg with { OptionLegAction = ParseForOptionLegAction(ddlLeg2Action) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg2Action, OptionType.Put, optionLeg);
    }

    void ddlLeg3Action_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg3Action, OptionType.Call);
        optionLeg = optionLeg with { OptionLegAction = ParseForOptionLegAction(ddlLeg3Action) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg3Action, OptionType.Call, optionLeg);
    }

    void ddlLeg4Action_SelectedIndexChanged(object sender, EventArgs e)
    {
        var optionLeg = _viewModel.GetOptionLeg(_viewModel.OptionLeg4Action, OptionType.Call);
        optionLeg = optionLeg with { OptionLegAction = ParseForOptionLegAction(ddlLeg4Action) };
        _viewModel.SetOptionLeg(_viewModel.OptionLeg4Action, OptionType.Call, optionLeg);
    }

    OptionType ParseForOptionType(ComboBox ddlOptionType)
        => (OptionType)Enum.Parse(typeof(OptionType), $"{ddlOptionType.SelectedItem}");

    OptionLegAction ParseForOptionLegAction(ComboBox ddlAction)
        => (OptionLegAction)Enum.Parse(typeof(OptionLegAction), $"{ddlAction.SelectedItem}");

    void txtFundBalance_TextChanged(object sender, EventArgs e)
    {
        _viewModel.FundBalance = ParseForDecimalAmount(txtFundBalance.Text);
        _viewModel.UpdateTradeLimitValues();
        ShowTradeValues();
    }

    void txtRiskMargin_TextChanged(object sender, EventArgs e)
    {
        _viewModel.IronCondorTrade.SetRiskMargin(ParseForDecimalAmount(txtRiskMargin.Text));
        _viewModel.UpdateTradeLimitValues();
        ShowTradeValues();
    }

    decimal ParseForDecimalAmount(string stringAmount)
    {
        var numberStyles = NumberStyles.AllowCurrencySymbol | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands;
        if (decimal.TryParse(stringAmount, numberStyles, CultureInfo.InstalledUICulture.NumberFormat, out decimal result))
            return result;
        return 0m;
    }

    void nudQuantity_ValueChanged(object sender, EventArgs e)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade) return;
        var quantity = Convert.ToInt32(nudQuantity.Value);
        for (var index = 0; index < _viewModel.OptionLegs.Length; index++)
        {
            var optionLeg = _viewModel.OptionLegs[index];
            optionLeg = _viewModel.GetOptionLeg(optionLeg.OptionLegAction, optionLeg.OptionLegType);
            optionLeg = optionLeg with { Quantity = quantity };
            _viewModel.OptionLegs[index] = optionLeg;

            var optionLegData = _viewModel.GetOptionLegData(
                optionLeg.OptionLegType == OptionType.Put ? _viewModel.PutSpreadTradeType: _viewModel.CallSpreadTradeType,
                _viewModel.TradeStatus, optionLeg.OptionLegAction, optionLeg.OptionLegType);
            optionLeg = optionLegData!.OptionLeg! with { Quantity = quantity };

            for (var legIndex = 0; legIndex < _viewModel.OptionLegData.Length; legIndex++)
            {
                var o = _viewModel.OptionLegData[legIndex];
                if (o.TradeType == optionLegData.TradeType 
                    && o.TradeStatus == optionLegData.TradeStatus
                    && o.OptionLegId == optionLeg.ContractId)
                {
                    _viewModel.OptionLegData[legIndex] = _viewModel.OptionLegData[legIndex].SetOptionLeg(optionLeg);
                    break;
                }
            }
        }

        _viewModel.UpdateOptionLegMap();
        _viewModel.UpdatePutCreditSpreadLiveFeedValues();
        _viewModel.UpdateCallCreditSpreadLiveFeedValues();
        ShowTradeValues();
    }

    void txtLeg1BidPrice_TextChanged(object sender, EventArgs e)
    {
        var bidPrice = ParseForDecimalAmount(txtLeg1BidPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put);
        if (optionLegData is not null && optionLegData.BidPrice != bidPrice)
        {
            optionLegData = optionLegData with { BidPrice = bidPrice };
            _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put, optionLegData);
            _viewModel.UpdatePutCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg1AskPrice_TextChanged(object sender, EventArgs e)
    {
        var askPrice = ParseForDecimalAmount(txtLeg1AskPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put);
        if (optionLegData is not null && optionLegData.AskPrice != askPrice)
        {
            optionLegData = optionLegData with { AskPrice = askPrice };
            _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg1Action, OptionType.Put, optionLegData);
            _viewModel.UpdatePutCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg2BidPrice_TextChanged(object sender, EventArgs e)
    {
        var bidPrice = ParseForDecimalAmount(txtLeg2BidPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put);
        if (optionLegData is not null && optionLegData.BidPrice != bidPrice)
        {
            optionLegData = optionLegData with { BidPrice = bidPrice};
            _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put, optionLegData);
            _viewModel.UpdatePutCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg2AskPrice_TextChanged(object sender, EventArgs e)
    {
        var askPrice = ParseForDecimalAmount(txtLeg2AskPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put);
        if (optionLegData is not null && optionLegData.AskPrice != askPrice)
        {
            optionLegData = optionLegData with { AskPrice = askPrice };
            _viewModel.SetOptionLegData(_viewModel.PutSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg2Action, OptionType.Put, optionLegData);
            _viewModel.UpdatePutCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg3BidPrice_TextChanged(object sender, EventArgs e)
    {
        var bidPrice = ParseForDecimalAmount(txtLeg3BidPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call);
        if (optionLegData is not null && optionLegData.BidPrice != bidPrice)
        {
            optionLegData = optionLegData with { BidPrice = bidPrice };
            _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call, optionLegData);
            _viewModel.UpdateCallCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg3AskPrice_TextChanged(object sender, EventArgs e)
    {
        var askPrice = ParseForDecimalAmount(txtLeg3AskPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call);
        if (optionLegData is not null && optionLegData.AskPrice != askPrice)
        {
            optionLegData = optionLegData with { AskPrice = askPrice };
            _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg3Action, OptionType.Call, optionLegData);
            _viewModel.UpdateCallCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg4BidPrice_TextChanged(object sender, EventArgs e)
    {
        var bidPrice = ParseForDecimalAmount(txtLeg4BidPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call);
        if (optionLegData is not null && optionLegData.BidPrice != bidPrice)
        {
            optionLegData = optionLegData with { BidPrice = bidPrice };
            _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call, optionLegData);
            _viewModel.UpdateCallCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void txtLeg4AskPrice_TextChanged(object sender, EventArgs e)
    {
        var askPrice = ParseForDecimalAmount(txtLeg4AskPrice.Text);
        var optionLegData = _viewModel.GetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call);
        if (optionLegData is not null && optionLegData.AskPrice != askPrice)
        {
            optionLegData = optionLegData with { AskPrice = askPrice };
            _viewModel.SetOptionLegData(_viewModel.CallSpreadTradeType, _viewModel.TradeStatus, _viewModel.OptionLeg4Action, OptionType.Call, optionLegData);
            _viewModel.UpdateCallCreditSpreadLiveFeedValues();
            ShowTradeValues();
        }
    }

    void ddlPrice_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (decimal.TryParse(ddlPrice.Text, out decimal orderPrice))
            _viewModel.OrderPrice = orderPrice;
    }

    void lblStrikePrice_Click(object sender, EventArgs e)
    {
        var dlg = new SetLocalSymbolForm(_viewModel.LocalSymbol);
        if (dlg.ShowDialog() == DialogResult.OK)
            _viewModel.LocalSymbol = dlg.LocalSymbol;
    }

    void ClearPrices()
    {
        txtLeg1AskPrice.Text = string.Empty;
        txtLeg1BidPrice.Text = string.Empty;
        txtLeg2AskPrice.Text = string.Empty;
        txtLeg2BidPrice.Text = string.Empty;
        txtLeg3AskPrice.Text = string.Empty;
        txtLeg3BidPrice.Text = string.Empty;
        txtLeg4AskPrice.Text = string.Empty;
        txtLeg4BidPrice.Text = string.Empty;
    }

    public void OrderActionTypeChanged(OrderActionType orderActionType)
    {
        if (_viewModel.FundOrderTrade.TradeState != TradeState.NewTrade)
            return;
        var controls = new Control[] { dtmLeg1LastTradeDate, dtmLeg2LastTradeDate, dtmLeg3LastTradeDate, dtmLeg4LastTradeDate,
            ddlLeg1StrikePrice, ddlLeg2StrikePrice, ddlLeg3StrikePrice, ddlLeg4StrikePrice, nudQuantity};
        switch (_viewModel.TradeType)
        {
            case TradeType.ShortIronCondor:
            case TradeType.LongIronCondor:
                _viewModel.UpdateOrderAction(orderActionType, () => this.Invoke(() => {
                    txtRiskMargin.Visible = false;
                    lblRiskMargin.Visible = false;
                    txtMaxLossLimit.Visible = false;
                    lblMaxLossLimit.Visible = false;
                    txtMaxProfit.Visible = false;
                    lblMaxProfit.Visible = false;
                    txtMaxReturn.Visible = false;
                    lblMaxReturn.Visible = false;
                    txtMaxProfitLimit.Visible = false;
                    lblMinProfitLimit.Visible = false;
                    txtMinProfitTarget.Visible = false;
                    lblMinProfitTarget.Visible = false;
                    ddlLeg1StrikePrice.SetSelectedIndex((int)_viewModel.GetParentOptionLeg(_viewModel.ParentOptionLeg1Action, OptionType.Put).StrikePrice);
                    ddlLeg2StrikePrice.SetSelectedIndex((int)_viewModel.GetParentOptionLeg(_viewModel.ParentOptionLeg2Action, OptionType.Put).StrikePrice);
                    ddlLeg3StrikePrice.SetSelectedIndex((int)_viewModel.GetParentOptionLeg(_viewModel.ParentOptionLeg3Action, OptionType.Call).StrikePrice);
                    ddlLeg4StrikePrice.SetSelectedIndex((int)_viewModel.GetParentOptionLeg(_viewModel.ParentOptionLeg4Action, OptionType.Call).StrikePrice);
                    txtLeg1BidPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentPutSpreadTradeType, _viewModel.ParentOptionLeg1Action, OptionType.Put)?.BidPrice ?? 0m:F2}";
                    txtLeg1AskPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentPutSpreadTradeType, _viewModel.ParentOptionLeg1Action, OptionType.Put)?.AskPrice ?? 0m:F2}";
                    txtLeg2BidPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentPutSpreadTradeType, _viewModel.ParentOptionLeg2Action, OptionType.Put)?.BidPrice ?? 0m:F2}";
                    txtLeg2AskPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentPutSpreadTradeType, _viewModel.ParentOptionLeg2Action, OptionType.Put)?.AskPrice ?? 0m:F2}";
                    txtLeg3BidPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentCallSpreadTradeType, _viewModel.ParentOptionLeg3Action, OptionType.Call)?.BidPrice ?? 0m:F2}";
                    txtLeg3AskPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentCallSpreadTradeType, _viewModel.ParentOptionLeg3Action, OptionType.Call)?.AskPrice ?? 0m:F2}";
                    txtLeg4BidPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentCallSpreadTradeType, _viewModel.ParentOptionLeg4Action, OptionType.Call)?.BidPrice ?? 0m:F2}";
                    txtLeg4AskPrice.Text = $"{GetParentOptionLegData(_viewModel.ParentCallSpreadTradeType, _viewModel.ParentOptionLeg4Action, OptionType.Call)?.AskPrice ?? 0m:F2}";
                    nudQuantity.Value = _viewModel.ParentTradeOrderQuantity;
                }));
                break;
        }
        return;

        OptionTradeLegDataReadModel GetParentOptionLegData(TradeType tradeType, OptionLegAction optionLegAction, OptionType optionType)
        {
            var optionLegData = _viewModel.GetParentOptionLegData(tradeType, TradeStatus.IntraDay, optionLegAction, optionType);
            optionLegData ??= _viewModel.GetParentOptionLegData(tradeType, TradeStatus.EndOfDay, optionLegAction, optionType);
            return optionLegData;
        }

    }

    void EnableControls(bool readOnly, Control[] controls)
    {
        if (controls is not null)
            foreach (var control in controls)
                ((TextBox)control).ReadOnly = readOnly;
    }

    void ddlRiskPosition_SelectedIndexChanged(object sender, EventArgs e)
    {
     }

    void btnSetRiskProfit_Click(object sender, EventArgs e)
    {
        txtRiskProfit.Text = "Loading...";
        _viewModel.SetFundMaxProfit();
    }
}
