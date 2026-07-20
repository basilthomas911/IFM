using System.Data;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.ViewModels.Trade;
using TomasAI.IFM.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.Views.Trade.IronCondor;

public partial class IronCondorView : UserControl, IFormControl
{
    Control _parentControl;
    IronCondorViewModel _viewModel;
    Dictionary<ActionState, Color> _tradePlanStateMap;

    /// <summary>
    /// Initializes a new instance of the <see cref="IronCondorView"/> class with the specified parent control and view
    /// model.
    /// </summary>
    /// <remarks>This constructor sets up the initial state of the view, including configuring UI elements
    /// such as the live feed dropdown and progress bar. The view is associated with a parent control for layout
    /// purposes and uses a view model to populate its data.</remarks>
    /// <param name="parentControl">The parent control that hosts this view. This control is used to manage layout and resizing.</param>
    /// <param name="viewModel">The view model that provides data and commands for the view. It must not be null.</param>
    public IronCondorView(Control parentControl, IronCondorViewModel viewModel)
    {
        InitializeComponent();
        ((IFormControl)this).Resize(parentControl);
        _parentControl = parentControl;
        _viewModel = viewModel;
        _tradePlanStateMap = new Dictionary<ActionState, Color> {
            { ActionState.Normal, Color.LimeGreen },
            { ActionState.Warning, Color.Yellow },
            { ActionState.Critical, Color.DarkOrange },
            { ActionState.RedAlert, Color.Red },
        };
        ddlLiveFeed.Enabled = false;
        ddlLiveFeed.Items.Clear();
        ddlLiveFeed.Items.AddRange(_viewModel.LiveFeedLabels);
        ddlLiveFeed.SelectedIndex = 0;
        pbPercentProfit.Style = ProgressBarStyle.Continuous;
        pnlRt.Visible = true;
    }

    /// <summary>
    /// Adjusts the size and layout of the control based on the specified parent control.
    /// </summary>
    /// <remarks>This method resizes the control to match the size of the <paramref name="parentControl"/> and
    /// adjusts the layout of internal panels and graphs accordingly.</remarks>
    /// <param name="parentControl">The parent control whose size is used to adjust the layout of this control.</param>
    void IFormControl.Resize(Control parentControl)
    {
        Size = parentControl.Size;
        pnlAssetSplitter.SplitterDistance = 680;
        pnlRealTimeData.Width = Width - pnlIronCondorTradeInfo.Width - 10;
        pnlRealTimeData.SplitterDistance = 495;
        pnlTradeSplitter.SplitterDistance = 495;
        var graphWidth = (Width - pnlIronCondorTradeInfo.Width) / 2;
        graphSpreadDistribution.Width = graphWidth;
        graphEodData.Width = graphWidth;
    }

    /// <summary>
    /// Opens the form control, making it ready for user interaction.
    /// </summary>
    /// <remarks>This method should be called to initialize the form control before any user input is
    /// processed. Ensure that any necessary preconditions are met before invoking this method.</remarks>
    void IFormControl.Open()
    {
    }

    /// <summary>
    /// Closes the form control by disabling live and market data feeds.
    /// </summary>
    /// <remarks>This method should be called to properly close the form control and ensure that all
    /// associated data feeds are disabled.</remarks>
    void IFormControl.Close()
    {
        _viewModel.DisableLiveFeed();
        _viewModel.DisableMarketDataFeedResetListener();
    }

    /// <summary>
    /// Initializes the Iron Condor control and sets up event handlers for loading trade data and updating the UI.
    /// </summary>
    /// <remarks>This method configures the UI components and binds various event handlers to the ViewModel's
    /// data loading events. It enables the trade history list, sets the trade description label, and prepares the
    /// control to display futures end-of-day data, trade information, and trade history. It also sets up the ability to
    /// reset the live feed and manage trade plans.</remarks>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void IronCondorControl_Load(object sender, EventArgs e)
    {
        lstTradePlanAction.SetDoubleBuffered(true);
        lstTradeHistory.SetDoubleBuffered(true);
        lstTradeHistory.Enabled = true;
        lblTradeDescription.Text = $"{_viewModel.Fund.Name} | {_viewModel.FundOrder.Reference ?? string.Empty}";
        _viewModel.ShowErrorMessage = (errorMsg, caption) => this.ShowErrorMessage(errorMsg, caption);

        _viewModel.OnFuturesEodDataHistoryLoaded = (futuresEodData) => 
            this.Post(() => {
                ShowFuturesEodDataHistory(futuresEodData);
                //graphEodData.DataBind();
                graphEodData.Series[0].Points.Clear();
                foreach (var e in futuresEodData)
                {
                    graphEodData.Series[0].Points.AddXY(e.ValueDate.ToDateTime(TimeOnly.MinValue), e.ClosePrice);
                    graphEodData.Series[1].Points.AddXY(e.ValueDate.ToDateTime(TimeOnly.MinValue), e.UpperBand);
                    graphEodData.Series[2].Points.AddXY(e.ValueDate.ToDateTime(TimeOnly.MinValue), e.Mean);
                    graphEodData.Series[3].Points.AddXY(e.ValueDate.ToDateTime(TimeOnly.MinValue), e.LowerBand);
                }
                graphEodData.ChartAreas[0].RecalculateAxesScale();
                graphEodData.Update();
                ddlLiveFeed.Enabled = true;
            });

        _viewModel.OnFuturesEodDataLoaded = futuresEodData => this.Post(() => {
            ShowFuturesEodData(futuresEodData);
            //graphEodData.Update();
        });

        _viewModel.OnTradeInfoLoaded = (tradeInfo) => this.Post(() => ShowTradeInfo(tradeInfo));
        _viewModel.OnTradeLimitsLoaded = (orderId, o) => this.Post(() => ShowTradeLimits(orderId, o));
        _viewModel.OnIronCondorTradePositionsLoaded = (key, ironCondorTradeData, tradeLimit, openingNetSpread, fundBalance) => this.Post(() => ShowIronCondorTradePosition(key, ironCondorTradeData, tradeLimit, openingNetSpread, fundBalance));
        _viewModel.OnIronCondorSpreadPathsLoaded = (key, ironCondorTradeData, tradeLimit, openingNetSpread, fundBalance) => this.Post(() => {
            ShowIronCondorTradePosition(key, ironCondorTradeData, tradeLimit, openingNetSpread, fundBalance);
            _viewModel.TradePositionUpdated();
        });

        _viewModel.OnOptionTradeSpreadBarDataLoaded = optionTradeSpreadBarUIData => this.Post(() => {
            ShowOptionTradeSpreadBarData(optionTradeSpreadBarUIData);
        });

        // Load the trade history and current trade history
        _viewModel.OnTradeHistoryLoaded = (tradeHistory) => this.Post(() =>  {
            var index = (tradeHistory?.Length ?? 0) - 1;
            if (index < 0 || index >= tradeHistory?.Length)
                return;
            ShowTradeHistory(tradeHistory!);
            if (!lstTradeHistory.Items[index].Selected)
                lstTradeHistory.Items[index].Selected = true;
            _viewModel.LoadIronCondorTradePosition(index);
            _viewModel.LoadOptionTradeSpreadBarData(index);
            lstTradePlanAction.Focus();
        });

        _viewModel.OnCurrentTradeHistoryLoaded = (tradeHistory) => this.Post(() => {
            ShowTradeHistory(tradeHistory);
            var index = tradeHistory.Length - 1;
            if (!lstTradeHistory.Items[index].Selected)
                lstTradeHistory.Items[index].Selected = true;
            lstTradePlanAction.Focus();
        });

        _viewModel.CanResetLiveFeed = (resetLiveFeed) => this.Post(() =>  
            resetLiveFeed(ddlLiveFeed.Text == IronCondorViewModel.LiveFeedOn));

        _viewModel.ShowTradePlan = o => this.Post(() => ShowTradePlan(o));
        _viewModel.ShowTradePlans = o => this.Post(() => ShowTradePlans(o));
        _viewModel.ClearTradePlans = () => this.Post( ClearTradePlans);
        _viewModel.EnableMarketDataFeedResetListener();

        _viewModel.LoadIronCondorTrade(
            onLoadComplete: (orderId, tradeId, trade) => {
                if (trade == null)
                {
                    this.Post(() => {
                        this.ShowErrorMessage($"No Iron Condor Trade found for orderId: {orderId} tradeId: {tradeId}", "Loading Trade");
                        _parentControl.Controls.Clear();
                    });
                }
                else
                    _viewModel.LoadIronCondorTrade(trade, orderId, tradeId);
            });

       }


    /// <summary>
    /// Displays an error message in a message box with the specified caption.
    /// </summary>
    /// <param name="errorMsg">The error message to display.</param>
    /// <param name="caption">The caption for the message box.</param>
    void ShowErrorMessage(string errorMsg, string caption) => this.Post(() => MessageBox.Show(text: errorMsg, caption: caption, buttons: MessageBoxButtons.OK, icon: MessageBoxIcon.Error));

    /// <summary>
    /// Updates the option leg data titles in the UI based on the specified trade type.
    /// </summary>
    /// <param name="pcsTradeType">The trade type for which to update the titles.</param>
    void ShowIronCondorTradeDataTitles(TradeType pcsTradeType)
    {
        var shortOptionLegAction = _viewModel.GetShortPutOptionLegAction(pcsTradeType);
        var longOptionLegAction = _viewModel.GetLongPutOptionLegAction(pcsTradeType);
        lblShortAsk.Text = $"{shortOptionLegAction} Ask";
        lblShortBid.Text = $"{shortOptionLegAction} Bid";
        lblShortDelta.Text = $"{shortOptionLegAction} Delta";
        lblShortGamma.Text = $"{shortOptionLegAction} Gamma";
        lblShortImpliedVolatility.Text = $"{shortOptionLegAction} IVol";
        lblShortStrike.Text = $"{shortOptionLegAction} Strike";
        lblShortTheta.Text = $"{shortOptionLegAction} Theta";

        lblLongAsk.Text = $"{longOptionLegAction} Ask";
        lblLongBid.Text = $"{longOptionLegAction} Bid";
        lblLongDelta.Text = $"{longOptionLegAction} Delta";
        lblLongGamma.Text = $"{longOptionLegAction} Gamma";
        lblLongImpliedVol.Text = $"{longOptionLegAction} IVol";
        lblLongStrike.Text = $"{longOptionLegAction} Strike";
        lblLongTheta.Text = $"{longOptionLegAction} Theta";

    }

    /// <summary>
    /// Populates the trade history list view with the provided trade history data.
    /// </summary>
    /// <param name="tradeHistory">An array of trade history view models to display.</param>
    void ShowTradeHistory(TradeHistoryReadModel[] tradeHistory)
    {
        lstTradeHistory.BeginUpdate();
        lstTradeHistory.Items.Clear();
        foreach (var e in tradeHistory)
        {
            lstTradeHistory.Items.Add(new ListViewItem(new string[] {
                $"{e.OrderId}",
                $"{e.TradeId}",
                $"{e.TradeType}",
                $"{e.ValueDate:yyyy-MM-dd}",
                $"{e.DaysToExpiry}",
                $"{e.TradeStatus}",
                e.Commission.HasValue ? $"{e.Commission.Value:F2}" : "",
                $"{Math.Abs(e.NetSpread):F2}",
                $"{e.TradePnl:F2}" }
            ));
        }
        if (tradeHistory.Length > 0)
            lstTradeHistory.EnsureVisible(tradeHistory.Length - 1);
        lstTradeHistory.EndUpdate();
    }

    /// <summary>
    /// Populates the trade info list view with the provided trade info data.
    /// </summary>
    /// <param name="tradeInfo">A collection of trade info view models to display.</param>
    void ShowTradeInfo(ICollection<TradeInfoReadModel> tradeInfo)
    {
        lstTradeInfo.Items.Clear();
        foreach (var e in tradeInfo)
        {
            var rowId = lstTradeInfo.Items.Add(new ListViewItem(new string[] {
                $"{e.OrderId}",
                $"{e.TradeId}",
                $"{e.TradeType}",
                $"{e.Quantity}",
                $"{e.TradeDate:yyyy-MM-dd}",
                $"{e.MaturityDate:yyyy-MM-dd}",
                $"{e.TradeState}",
                $"{e.TradeAction}" }
            ));
        }
        if (lstTradeInfo.Items.Count > 0)
            ShowOptionLegContractIds(0);
    }

    /// <summary>
    /// Displays the option leg contract IDs for the specified row in the trade info list.
    /// </summary>
    /// <param name="rowId">The index of the row for which to display contract IDs.</param>
    void ShowOptionLegContractIds(int rowId)
    {
        lstOptionContractIds.Items.Clear();
        foreach (var contractId in _viewModel.GetOptionLegContractIds())
            lstOptionContractIds.Items.Add(contractId);
    }

    /// <summary>
    /// Displays the trade limits and fund balance for the specified order.
    /// </summary>
    /// <param name="orderId">The order ID.</param>
    /// <param name="e">A tuple containing the trade limit view model and fund balance.</param>
    void ShowTradeLimits(int orderId, (TradeLimitReadModel TradeLimit, decimal FundBalance) e)
    {
        lstTradeLimit.Items.Clear();
        if (e.TradeLimit != null)
        {
            var tradeLimitItem = new ListViewItem(new string[] {
                $"{orderId}",
                $"{e.TradeLimit.RiskMargin:C}",
                $"{e.TradeLimit.MaxProfit:C}",
                $"{e.TradeLimit.MaxLoss:C}",
                $"{e.TradeLimit.MaxReturn:P2}",
                $"{e.TradeLimit.MaxLossLimit:F4}",
                $"{e.TradeLimit.MaxProfitLimit:F4}",
                $"{e.TradeLimit.MinProfitTarget:C}",
                $"{e.TradeLimit.DailyProfitTarget:C}",
                $"{e.FundBalance:C}"
            });
            lstTradeLimit.Items.Add(tradeLimitItem);
        }
    }

    class ContractIdViewModel(string contractId)
    {
        readonly string _contractId = contractId;
        public string ContractId => _contractId;
    }

    /// <summary>
    /// Displays the end-of-day futures data in the real-time data section.
    /// </summary>
    /// <param name="e">The futures EOD data view model to display.</param>
    void ShowFuturesEodData(FuturesEodDataV2ReadModel e)
    {
        txtRtValueDate.Text = $"{e.ValueDate:yyyy-MMM-dd}";
        txtRtAssetPrice.Text = $"{e.ClosePrice:F2}";
        txtRtAssetPrice.BackColor = e.ClosePrice >= e.OpenPrice ? Color.LimeGreen : Color.Red;
    }

    /// <summary>
    /// Populates the futures EOD data history list view with the provided data.
    /// </summary>
    /// <param name="eodData">An array of futures EOD data view models to display.</param>
    void ShowFuturesEodDataHistory(FuturesEodDataV2ReadModel[] eodData)
    {
        if (eodData == null || eodData.Length == 0)
        {
            this.lstFuturesEodData.Items.Clear();
            return;
        }
        lstFuturesEodData.BeginUpdate();
        foreach (var e in eodData)
            lstFuturesEodData.Items.Add(new ListViewItem(new string[] {
                $"{e.ValueDate:yyyy-MM-dd}",
                $"{e.OpenPrice:F2}",
                $"{e.HighPrice:F2}",
                $"{e.LowPrice:F2}",
                $"{e.ClosePrice:F2}",
                $"{e.Volume:F0}",
                $"{e.DailyPercentChange:P}",
                $"{e.DailyStdDev:F4}",
                $"{e.UpperBand:F2}",
                $"{e.Mean:F2}",
                $"{e.LowerBand:F2}",
                $"{e.MarketDirection}",
                $"{e.MarketVolatility}",
                $"{e.PriceDirection}",
                $"{e.PriceVolatility}"
            }));
        this.futuresEodDataViewModelBindingSource.DataSource = eodData;
        this.futuresEodDataViewModelBindingSource.ResetBindings(true);
        lstFuturesEodData.EndUpdate();
    }

    /// <summary>
    /// Displays the iron condor trade position details in the UI, including both put and call credit spreads, trade limits, and fund balance.
    /// </summary>
    /// <param name="key">The trade position entity identifier.</param>
    /// <param name="ironCondorTradeData">A tuple containing the put and call credit spread view models.</param>
    /// <param name="tradeLimit">The trade limit view model.</param>
    /// <param name="openingNetSpread">The opening net spread value.</param>
    /// <param name="fundBalance">The fund balance value.</param>
    void ShowIronCondorTradePosition(TradePositionEntityId key, (TradePositionReadModel PutCreditSpread, TradePositionReadModel CallCreditSpread) ironCondorTradeData, TradeLimitReadModel tradeLimit, decimal openingNetSpread, decimal fundBalance)
    {
       // if (_viewModel.IsLiveFeedEnabled && key.TradeStatus != TradeStatus.IntraDay) return;
        var pcs = ironCondorTradeData.PutCreditSpread;
        txtPutSpreadType.Text = $"{ironCondorTradeData.PutCreditSpread.TradeType}";
        txtPutSpreadType.ForeColor = Color.Magenta;
        if (pcs?.OptionLegData?.Length == 2)
        {
            var shortPutOptionLeg = pcs.OptionLegData.Where(o => o.OptionLeg!.OptionLegAction == _viewModel.GetShortPutOptionLegAction(pcs.TradeType)).SingleOrDefault();
            var longPutOptionLeg = pcs.OptionLegData.Where(o => o.OptionLeg!.OptionLegAction == _viewModel.GetLongPutOptionLegAction(pcs.TradeType)).SingleOrDefault();
            txtPutShortStrike.Text = $"{shortPutOptionLeg!.OptionLeg!.StrikePrice:F0}";
            txtPutShortStrike.BackColor = Color.Aqua;
            txtPutShortStrike.ForeColor = Color.Black;
            SetBGColor(txtPutShortBid, shortPutOptionLeg.BidPrice, txtPutShortBid.Text, "F2");
            SetBGColor(txtPutShortAsk, shortPutOptionLeg.AskPrice, txtPutShortAsk.Text, "F2");
            SetBGColor(txtPutShortDelta, shortPutOptionLeg.Delta, txtPutShortDelta.Text, "0.###0");
            SetBGColor(txtPutShortGamma, shortPutOptionLeg.Gamma, txtPutShortGamma.Text, "0.####0");
            SetBGColor(txtPutShortTheta, shortPutOptionLeg.Theta, txtPutShortTheta.Text, "0.###0");
            SetBGColor(txtPutShortImpliedVol, shortPutOptionLeg.ImpliedVolatility, $"{ToDoublePercent(txtCallLongImpliedVol.Text.Replace("%", ""))}", "P");
            txtPutLongStrike.Text = $"{longPutOptionLeg!.OptionLeg!.StrikePrice:F0}";
            txtPutLongStrike.BackColor = Color.Aqua;
            txtPutLongStrike.ForeColor = Color.Black;
            SetBGColor(txtPutLongBid, longPutOptionLeg.BidPrice, txtPutLongBid.Text, "F2");
            SetBGColor(txtPutLongAsk, longPutOptionLeg.AskPrice, txtPutLongAsk.Text, "F2");
            SetBGColor(txtPutLongDelta, longPutOptionLeg.Delta, txtPutLongDelta.Text, "0.###0");
            SetBGColor(txtPutLongGamma, longPutOptionLeg.Gamma, txtPutLongGamma.Text, "0.####0");
            SetBGColor(txtPutLongTheta, longPutOptionLeg.Theta, txtPutLongTheta.Text, "0.###0");
            SetBGColor(txtPutLongImpliedVol, longPutOptionLeg.ImpliedVolatility, $"{ToDoublePercent(txtPutLongImpliedVol.Text.Replace("%", ""))}", "P");
            SetBGColor(txtPutNetSpread, Math.Abs(pcs.NetSpread), txtPutNetSpread.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtPutTradeValue, Math.Abs(pcs.TradeValue), txtPutTradeValue.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtPutTradePnl, pcs.TradePnl, txtPutTradePnl.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtPutOTMProbability, pcs.OTMProbability, $"{ToDoublePercent(txtPutOTMProbability.Text.Replace("%", ""))}", "P");
            SetBGColor(txtPutForwardPrice, Math.Abs(pcs.ForwardPrice), txtPutForwardPrice.Text, "F2");
        }

        var ccs = ironCondorTradeData.CallCreditSpread;
        txtCallSpreadType.Text = $"{ironCondorTradeData.CallCreditSpread.TradeType}";
        txtCallSpreadType.ForeColor = Color.Magenta;
        if (ccs?.OptionLegData?.Length == 2)
        {
            var shortCallOptionLeg = ccs.OptionLegData.Where(o => o.OptionLeg!.OptionLegAction == _viewModel.GetShortCallOptionLegAction(ccs.TradeType)).SingleOrDefault();
            var longCallOptionLeg = ccs.OptionLegData.Where(o => o.OptionLeg!.OptionLegAction == _viewModel.GetLongCallOptionLegAction(ccs.TradeType)).SingleOrDefault();
            txtCallShortStrike.Text = $"{shortCallOptionLeg!.OptionLeg!.StrikePrice:F0}";
            txtCallShortStrike.BackColor = Color.Aqua;
            txtCallShortStrike.ForeColor = Color.Black;
            SetBGColor(txtCallShortBid, shortCallOptionLeg.BidPrice, txtCallShortBid.Text, "F2");
            SetBGColor(txtCallShortAsk, shortCallOptionLeg.AskPrice, txtCallShortAsk.Text, "F2");
            SetBGColor(txtCallShortDelta, shortCallOptionLeg.Delta, txtCallShortDelta.Text, "0.###0");
            SetBGColor(txtCallShortGamma, shortCallOptionLeg.Gamma, txtCallShortGamma.Text, "0.####0");
            SetBGColor(txtCallShortTheta, shortCallOptionLeg.Theta, txtCallShortTheta.Text, "0.###0");
            SetBGColor(txtCallShortImpliedVol, shortCallOptionLeg.ImpliedVolatility, $"{ToDoublePercent(txtCallShortImpliedVol.Text.Replace("%", ""))}", "P");
            txtCallLongStrike.Text = $"{longCallOptionLeg!.OptionLeg!.StrikePrice:F0}";
            txtCallLongStrike.BackColor = Color.Aqua;
            txtCallLongStrike.ForeColor = Color.Black;
            SetBGColor(txtCallLongBid, longCallOptionLeg.BidPrice, txtCallLongBid.Text, "F2");
            SetBGColor(txtCallLongAsk, longCallOptionLeg.AskPrice, txtCallLongAsk.Text, "F2");
            SetBGColor(txtCallLongDelta, longCallOptionLeg.Delta, txtCallLongDelta.Text, "0.###0");
            SetBGColor(txtCallLongGamma, longCallOptionLeg.Gamma, txtCallLongGamma.Text, "0.####0");
            SetBGColor(txtCallLongTheta, longCallOptionLeg.Theta, txtCallLongTheta.Text, "0.###0");
            SetBGColor(txtCallLongImpliedVol, longCallOptionLeg.ImpliedVolatility, $"{ToDoublePercent(txtCallLongImpliedVol.Text.Replace("%", ""))}" , "P");
            SetBGColor(txtCallNetSpread, Math.Abs(ccs.NetSpread), txtCallNetSpread.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtCallTradeValue, Math.Abs(ccs.TradeValue), txtCallTradeValue.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtCallTradePnl, ccs.TradePnl, txtCallTradePnl.Text.Replace("$", "").Replace(",", ""), "C");
            SetBGColor(txtCallOTMProbability, ccs.OTMProbability, $"{ToDoublePercent(txtCallOTMProbability.Text.Replace("%", ""))}", "P");
            SetBGColor(txtCallForwardPrice, Math.Abs(ccs.ForwardPrice), txtCallForwardPrice.Text, "F2");
        }
        if (pcs is not null)
            ShowIronCondorTradeDataTitles(pcs.TradeType);
        var dailyPnl = (pcs?.TradePnl ?? 0m) + (ccs?.TradePnl ?? 0m);
        var netSpread = (pcs?.NetSpread ?? 0m) + (ccs?.NetSpread ?? 0m);
        txtRtValueDate.Text = $"{key.ValueDate:yyyy-MMM-dd}";
        txtRtTradeStatus.Text = $"{key.TradeStatus}";
        txtRtDaysToExpiry.Text = $"{key.DaysToExpiry}";
        txtRtTradePnl.Text =  $"{dailyPnl:C}";
        txtRtTradePnl.BackColor = dailyPnl >= 0.0m ? Color.LimeGreen : Color.Red;
        txtRtNetSpread.Text =  $"{Math.Abs(netSpread):C}";
        txtRtNetSpread.BackColor = netSpread > openingNetSpread && openingNetSpread > 0.0m ? Color.Red : Color.LimeGreen;

        //var tradePnl = _viewModel.GetTradePnl(pcs, ccs, pcs.TradeStatus == TradeStatus.Close ? -1 : 1);
        var tradePnl = _viewModel.GetTradePnl();
        var tradePnlValue = Convert.ToInt32(tradePnl);
        if (tradePnlValue >= 0m)
        {
            var maxProfit = Convert.ToInt32(tradeLimit.MaxProfit);
            tradePnlValue = tradePnlValue < maxProfit ? tradePnlValue : maxProfit;
            DisplayPercentProfit(maxProfit);
        }
        else if (tradePnlValue < 0m)
        {
            var maxLoss = Convert.ToInt32(tradeLimit.MaxLoss);
            tradePnlValue = tradePnlValue > maxLoss ? tradePnlValue :maxLoss ;
            DisplayPercentLoss(maxLoss);
        }
        ddlLiveFeed.Enabled = _viewModel.ValueDate.HasValue;
        Task.Delay(100).Wait();
        return;

        double ToDoublePercent(string percentText)
            =>string.IsNullOrWhiteSpace(percentText)
                ? 0.0
                : Convert.ToDouble(percentText.Replace("%", "")) / 100;

        void DisplayPercentProfit(int maxProfit)
        {
            pbPercentProfit.SetState(1);
            pbPercentProfit.Visible = true;
            pbPercentProfit.Maximum = (int )fundBalance;
            pbPercentProfit.Value = tradePnlValue;
             var percent = (((double)pbPercentProfit.Value / (double)pbPercentProfit.Maximum));
            if (percent < 0.0)
                percent = 0.0;
            txtTradePnl.Text = $"{tradePnl:C} @ {percent:P2} profit";
            txtTradePnl.BackColor = Color.LimeGreen;
            pbPercentProfit.Refresh();
        }

        void DisplayPercentLoss(int maxLoss)
        {
            pbPercentProfit.SetState(2);
            pbPercentProfit.Visible = true;
            pbPercentProfit.Maximum = Math.Abs((int)fundBalance);
            pbPercentProfit.Value = Math.Abs(tradePnlValue);
            var percent = (((double)pbPercentProfit.Value / (double)pbPercentProfit.Maximum));
            if (percent < 0.0)
                percent = 0.0;
            txtTradePnl.Text =$"{tradePnl:C} @ {percent:P2} loss";
            txtTradePnl.BackColor = Color.Red;
            pbPercentProfit.Refresh();
        }


        void SetBGColor(TextBox e, object newValue, string curValue, string valueFormat)
        {
            var textValue = string.Empty;
            var bgColor = Color.FromKnownColor(KnownColor.Black);
            var fontStyle = FontStyle.Regular;
            switch (newValue)
            {
                case decimal newDecimalValue:
                    var curDecimalValue = Convert.ToDecimal(string.IsNullOrWhiteSpace(curValue) ? "0" : curValue);
                    if (newDecimalValue != 0 && newDecimalValue != curDecimalValue)
                    {
                        bgColor = newDecimalValue > curDecimalValue ? Color.LimeGreen : Color.Red;
                        fontStyle = FontStyle.Bold;
                    }
                    textValue = newDecimalValue.ToString(valueFormat);
                    break;
                case double newDoubleValue:
                    var curDoubleValue = Convert.ToDouble(string.IsNullOrWhiteSpace(curValue) ? "0" : curValue);
                    if (newDoubleValue != 0 && newDoubleValue != curDoubleValue)
                    {
                        bgColor = newDoubleValue > curDoubleValue ? Color.LimeGreen : Color.Red;
                        fontStyle = FontStyle.Bold;
                    }
                    textValue = newDoubleValue.ToString(valueFormat);
                    break;
            }
            e.Font = new Font(e.Font, fontStyle);
            e.Text = textValue;
            e.BackColor = bgColor;
            e.Update();
            e.Refresh();
        }
    }

    /// <summary>
    /// Displays the option trade spread bar data in the spread distribution graph.
    /// </summary>
    /// <param name="optionTradeSpreadBarData">A collection of option trade spread bar UI view models to display.</param>
    void ShowOptionTradeSpreadBarData(ICollection<OptionTradeSpreadBarUIViewModel> optionTradeSpreadBarData)
    {
        graphSpreadDistribution.Series.SuspendUpdates();
        graphSpreadDistribution.Series[0].Points.Clear();
        graphSpreadDistribution.Series[1].Points.Clear();
        graphSpreadDistribution.Series[2].Points.Clear();
        graphSpreadDistribution.Series[3].Points.Clear();
        graphSpreadDistribution.Series[4].Points.Clear();
        foreach (var e in optionTradeSpreadBarData)
        {
            graphSpreadDistribution.Series[0].Points.AddXY(e.BarDate, e.LossLimit);
            graphSpreadDistribution.Series[1].Points.AddXY(e.BarDate, e.WinLimit);
            graphSpreadDistribution.Series[2].Points.AddXY(e.BarDate, e.ForwardSpread);
            graphSpreadDistribution.Series[3].Points.AddXY(e.BarDate, e.NetSpread);
            graphSpreadDistribution.Series[4].Points.AddXY(e.BarDate, e.MDIWarningLimit);
        }
        graphSpreadDistribution.ChartAreas[0].RecalculateAxesScale();
        graphSpreadDistribution.Series.ResumeUpdates();
    }

    /// <summary>
    /// Displays a single trade plan in the trade plan action list and updates related UI elements.
    /// </summary>
    /// <param name="e">The trade plan view model to display.</param>
    void ShowTradePlan(TradePlanReadModel e)
    {
        var bgColor = e.TradeType == TradeType.ShortIronCondor
            ? e.MScore switch {
                >= 0.9 => Color.Red,
                >= 0.8 => Color.Yellow,
                _ => Color.LimeGreen }
            : e.MScore switch {
                >= 0.9 => Color.LimeGreen,
                >= 0.8 => Color.Yellow,
                _ => Color.Red };
        txtRtMscore.BackColor =  bgColor;
        txtRtMscore.Text = $"{e.MScore:P2}";
        pnlTradePlanAction.BackColor = _tradePlanStateMap[e.ActionState];
        DisplayTradePlan(e);
        DisplayTradePlanActionMessage($"{e.ActionType}");
        DisplayTradePlanActionReasonMessage($"{e.ActionReason ?? string.Empty}");
        return;

        void DisplayTradePlan(TradePlanReadModel e)
        {
            lstTradePlanAction.BeginUpdate();
            var tradePlanActionItem = new ListViewItem(new string[] {
                $"{e.ActionDate:T}",
                $"{e.ActionType}",
                $"{e.ActionSubType}",
                $"{e.ActionState}",
                e.ActionReason ?? string.Empty,
                $"{e.TrendType}",
                $"{e.TrendStrength}",
                $"{e.RSI:F2}",
                $"{e.RSISlope:F4}",
                $"{e.TDI}",
                $"{e.TDIStrength}",
                $"{e.TradePnl:F2}",
                $"{e.ForwardLossRatio:F4}",
                $"{e.MScore:P2}",
                $"{e.NetPrice:F2}",
                $"{e.ForwardPrice:F2}",
                $"{e.AssetPrice:F2}",
                $"{e.StopLossLimit:P2}"
            });
            lstTradePlanAction.Items.Insert(0, tradePlanActionItem);
            lstTradePlanAction.EndUpdate();
        }

        void DisplayTradePlanActionMessage(string message)
        {
            pnlTradePlanAction.Refresh();
            var font = new Font("Microsoft Sans Serif", 12.0f, FontStyle.Bold);
            var gfx = pnlTradePlanAction.CreateGraphics();
            var messageSize = gfx.MeasureString(message, font);
            gfx.DrawString(message,
                font,
                Brushes.Black,
                new PointF(10.0f, ((float)(pnlTradePlanAction.Height) - messageSize.Height) / 2.0f));
        }

        void DisplayTradePlanActionReasonMessage(string message)
        {
            pnlTradePlanActionReason.Refresh();
            var font = new Font("Microsoft Sans Serif", 12.0f, FontStyle.Bold);
            var gfx = pnlTradePlanActionReason.CreateGraphics();
            var messageSize = gfx.MeasureString(message, font);
            gfx.DrawString(message,
                font,
                Brushes.White,
                new PointF(10.0f, ((float)(pnlTradePlanActionReason.Height) - messageSize.Height) / 2.0f));
        }
    }

    void ClearTradePlans()
    {
        lstTradePlanAction.BeginUpdate();
        lstTradePlanAction.Items.Clear();
        lstTradePlanAction.EndUpdate();
    }

    /// <summary>
    /// Displays the specified trade plans in the trade plan action list.
    /// </summary>
    /// <param name="tradePlans">The trade plans to display.</param>
    void ShowTradePlans(TradePlanReadModel[] tradePlans)
    {
        if (tradePlans.Length == 0)
            return;
        lstTradePlanAction.BeginUpdate();
        lstTradePlanAction.Items.Clear();
        foreach (var e in tradePlans)
        {
            var tradePlanActionItem = new ListViewItem([
                $"{e.ActionDate:T}",
                $"{e.ActionType}",
                $"{e.ActionSubType}",
                $"{e.ActionState}",
                e.ActionReason ?? string.Empty,
                $"{e.TrendType}",
                $"{e.TrendStrength}",
                $"{e.RSI:F2}",
                $"{e.RSISlope:F4}",
                $"{e.TDI}",
                $"{e.TDIStrength}",
                $"{e.TradePnl:F2}",
                $"{e.ForwardLossRatio:F4}",
                $"{e.MScore:P2}",
                $"{e.NetPrice:F2}",
                $"{e.ForwardPrice:F2}",
                $"{e.AssetPrice:F2}",
                $"{e.StopLossLimit:P2}"
            ]);
            lstTradePlanAction.Items.Add(tradePlanActionItem);
        }
        lstTradePlanAction.EndUpdate();
    }

    /// <summary>
    /// Calculates the number of days to expiry between the value date and trade date.
    /// </summary>
    /// <param name="valueDate">The value date.</param>
    /// <param name="tradeDate">The trade date.</param>
    /// <returns>The number of days to expiry.</returns>
    int GetDaysToExpiry(DateOnly valueDate, DateOnly tradeDate)
        => (tradeDate.DayNumber - valueDate.DayNumber);

    /// <summary>
    /// Handles the event when the live feed dropdown selection changes, enabling or disabling the live feed accordingly.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void ddlLiveFeed_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (!ddlLiveFeed.Enabled) return;
        switch ($"{ddlLiveFeed.SelectedItem}")
        {
            case IronCondorViewModel.LiveFeedOn:
                ddlLiveFeed.Font = new Font(ddlLiveFeed.Font, FontStyle.Bold);
                _viewModel.EnableLiveFeed();
                break;
            case IronCondorViewModel.LiveFeedOff:
                ddlLiveFeed.Font = new Font(ddlLiveFeed.Font, FontStyle.Regular);
                _viewModel.DisableLiveFeed();
                break;
        }
    }

    /// <summary>
    /// Handles the click event for the low real-time label. (Currently not implemented)
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void lblLowRT_Click(object sender, EventArgs e)
    {

    }

    /// <summary>
    /// Handles the cell formatting event for the real-time trade data grid, setting the background color.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The cell formatting event arguments.</param>
    void gridRtTradeData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        e.CellStyle.BackColor = SystemColors.ControlDarkDark;
    }

    /// <summary>
    /// Handles the event when the selected index changes in the trade history list, loading the corresponding trade data.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void lstTradeHistory_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (lstTradeHistory.SelectedIndices.Count != 1 || !lstTradeHistory.Enabled) return;
        var index = lstTradeHistory.SelectedIndices[0];
        _viewModel.LoadIronCondorTradePosition(index);
        _viewModel.LoadOptionTradeSpreadBarData(index);
        _viewModel.LoadTradePlans(index);
        _viewModel.LoadFuturesEodData(index);
    }

    /// <summary>
    /// Handles the event when the selected index changes in the trade info list, displaying the option leg contract IDs for the selected row.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void lstTradeInfo_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.GetTradeInfoCount() > 0 && lstTradeInfo.SelectedIndices.Count > 0)
        {
            var rowId = lstTradeInfo.SelectedIndices[0];
            ShowOptionLegContractIds(rowId);
        }
    }

    /// <summary>
    /// Handles the event when the IronCondorView control is removed. (Currently not implemented)
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The control event arguments.</param>
    void IronCondorView_ControlRemoved(object sender, ControlEventArgs e)
    {

    }

    /// <summary>
    /// Handles the click event for the VIX volatility real-time label. (Currently not implemented)
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void lblVixVolRT_Click(object sender, EventArgs e)
    {

    }
}
