using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Log;
using TomasAI.IFM.Views.Trade.IronCondor;
using TomasAI.IFM.ViewModels.Fund;
using TomasAI.IFM.ViewModels.Trade;
using TomasAI.IFM.ViewModels.Trade.IronCondor;
using QLNet;

namespace TomasAI.IFM.Views.Trade
{
    public partial class TradeOrderEditorForm : Form, IForm<TradeOrderEditorForm>, IFormControl
    {
        readonly IAppRoot _appRoot;
        TradeOrderEditorViewModel _viewModel;
        int _lastTradeIndex;
        int _lastTradeOrderIndex;
   
        /// <summary>
        /// create trade order form
        /// </summary>
        /// <param name="appRoot"></param>
        public TradeOrderEditorForm(IAppRoot appRoot)
        {
            _appRoot = appRoot;
            InitializeComponent();
        }

        public FundReadModel Fund => _viewModel.GetFund(ddlFund.SelectedIndex);

        public FundOrderReadModel FundOrder => _viewModel.FundOrders != null && _viewModel.FundOrders.Count > 0 && lstTradeOrders.SelectedItems.Count > 0
            ? _viewModel.GetFundOrder(lstTradeOrders.SelectedItems[0].Index)
            : default;

        public FundOrderTradeReadModel FundOrderTrade => _viewModel.FundOrderTrades != null && _viewModel.FundOrderTrades.Count > 0 && lstTrades.SelectedItems.Count > 0
            ? _viewModel.GetFundOrderTrade(lstTrades.SelectedItems[0].Index)
            : default;

        /// <summary>
        /// load view model
        /// </summary>
        /// <param name="viewModel"></param>
        public void LoadViewModel(TradeOrderEditorViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.OnError = (errorCode, errorMsg) => this.ShowErrorMessage(errorMsg, "Trade Order Editor Error");
            _viewModel.ShowErrorMessage = (errorMsg, errorCaption) => this.ShowErrorMessage(errorMsg, errorCaption);
            _viewModel.StartWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.WaitCursor);
            _viewModel.StopWaitIndicator = () => this.Post(() => Cursor.Current = Cursors.Default);
            _viewModel.OnShowButtons = () => this.Post(() => {

                btnCreateFund.Enabled = true;
                btnLoadOrder.Enabled = lstTradeOrders.Items.Count > 0;
                btnCreateOrder.Enabled = ddlFund.Items.Count > 0;
                btnDeleteOrder.Enabled = lstTradeOrders.Items.Count > 0;
                btnAddTrade.Enabled = lstTradeOrders.Items.Count > 0;
                btnRemoveTrade.Enabled = lstTrades.Items.Count > 0;
                btnEndOfDay.Enabled = (FundOrder?.OrderStatus ?? Shared.Fund.OrderStatus.Closed) == Shared.Fund.OrderStatus.Open;
                btnSubmitOrder.Enabled = (FundOrderTrade?.TradeState ?? TradeState.NewTrade) == TradeState.NewTrade;
                cbLiveFeed.Enabled = (FundOrderTrade?.TradeState ?? TradeState.NewTrade) == TradeState.NewTrade;
                var fundOrder = _viewModel.GetFundOrder(_viewModel.FundOrderSelectedIndex);
                if (fundOrder is not null)
                {
                    var orderCompleted = fundOrder.OrderStatus == Shared.Fund.OrderStatus.Closed;
                    btnCompleteOrder.Enabled = !orderCompleted;
                    if (orderCompleted)
                    {
                        btnAddTrade.Enabled = false;
                        btnRemoveTrade.Enabled = false;
                        btnEndOfDay.Enabled = false;
                        btnSubmitOrder.Enabled = false;
                        cbLiveFeed.Enabled = false;
                    }
                }
            });

            _viewModel.OnFundsLoaded = () => this.Post(() => {
                ddlFund.Items.Clear();
                if (_viewModel.Funds != null && _viewModel.Funds.Count > 0)
                {
                    btnCreateOrder.Enabled = true;
                    ddlFund.SelectedIndex = -1;
                    foreach (var fund in _viewModel.Funds)
                        ddlFund.Items.Add(fund.Name);
                    ddlFund.SelectedIndex = _viewModel.FundSelectedIndex;
                }
            });
            _viewModel.OnOrderAddedToFund = e => {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"OrderCreated: {e.FundOrder.OrderId} {e.FundOrder.Reference}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnOrderRemovedFromFund = e => {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"OrderRemoved: {e.FundOrderId.OrderId}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnFundOrderTradeStateChanged = (e, tradeState) => {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"OrderStateChanged: {e.OrderId}:{e.TradeId} {tradeState}");
                this.Post(() =>
                {
                    Task.Delay(TimeSpan.FromSeconds(1));
                    DialogResult = DialogResult.OK;
                    Close();
                    LoadFunds();
                });
            };
            _viewModel.OnTradeAddedToFundOrder = e => {
                _viewModel.AddTradeLiveFeed(e.FundOrderTrade.OrderId, e.FundOrderTrade.TradeId);
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"TradeAdded: {e.FundOrderTrade.TradeId} {e.FundOrderTrade.Reference ?? string.Empty}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnTradesRemovedFromFundOrder = e => {
                _viewModel.RemoveTradeLiveFeeds(e.FundOrderTradeId.OrderId);
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"TradesRemoved: all trades removed from order: {e.FundOrderTradeId.OrderId}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnTradeRemovedFromFundOrder = e =>
            {
                _viewModel.RemoveTradeLiveFeed(e.FundOrderTradeId.OrderId, e.FundOrderTradeId.TradeId);
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"TradeRemoved: {e.FundOrderTradeId.TradeId} was removed from order: {e.FundOrderTradeId.OrderId}");
                this.Post(() =>
                {
                    _lastTradeOrderIndex = lstTradeOrders.SelectedIndices.Count > 0 ? lstTradeOrders.SelectedIndices[0] : -1;
                    _lastTradeIndex = -1;
                    LoadFunds();
                });
            };
            _viewModel.OnOrderSubmitted = e =>
            {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"OrderSubmitted: {e.OrderAction} {e.OrderQuantity} {e.TradeType} {e.OrderDescription}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnEndOfDayProcessed = e =>
            {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"End Of Day Process completed successfully for Value Date: {e.FundTransaction.ValueDate:yyyy-MMM-dd}");
                this.Post(() => LoadFunds());
            };
            _viewModel.OnOptionTradeOrderPlaced = (orderId, tradeId) =>
            {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"Option Trade Order completed successfully for Value Date: {orderId}:{tradeId})");
                this.Post(() => LoadTradeOrder());
                //this.Post(() => LoadFunds());
            };
            _viewModel.OnFundOrderClosed = e =>
            {
                _viewModel.WriteStatusConsole(LogSourceType.TradeOrder, $"OrderClosed: {e.FundOrderId.OrderId} was successfully closed)");
                this.Post(() => LoadFunds());
            };
        }

        public void SetOrderAction(OrderActionType orderActionType)
        {
            ddlOrderActionType.SelectedItem = $"{orderActionType}";
        }

        void TradeOrderForm_Load(object sender, EventArgs e)
        {
            _lastTradeIndex = -1;
            _lastTradeOrderIndex = -1;
            
            dtpTradeDate.Value = _viewModel.ValueDate.HasValue ? _viewModel.ValueDate.Value : new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            btnLoadOrder.Enabled = false;
            btnCreateOrder.Enabled = false;
            btnDeleteOrder.Enabled = false;
            btnCompleteOrder.Enabled = false;
            var dtpList = new List<DateTimePicker> { dtpFrom, dtpTo };
            dtpList.ForEach(o => o.Enabled = false);
            dtpFrom.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month), 23, 59, 59);
            dtpList.ForEach(o => o.Enabled = true);
            LoadOrderActionTypes();
            LoadFunds();
            _viewModel.StartFundOrderListener();
        }

        void TradeOrderEditorForm_Shown(object sender, EventArgs e)
        {
        }

        void TradeOrderEditorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _viewModel.StopFundOrderListener();
        }

        void LoadOrderActionTypes()
        {
            ddlOrderActionType.Enabled = false;
            ddlOrderActionType.Items.Clear();
            ddlOrderActionType.Items.Add($"{OrderActionType.Open}");
            ddlOrderActionType.Items.Add($"{OrderActionType.Close}");
            ddlOrderActionType.SelectedIndex = 0;
        }

        void LoadFunds()
        {
            _lastTradeIndex = -1;
            _lastTradeOrderIndex = -1;
            pnlTradeControl.Controls.Clear();
            ddlFund.Items.Clear();
            lstTradeOrders.Items.Clear();
            lstTrades.Items.Clear();
            _viewModel.LoadFunds();
        }

        void LoadFunds(Action onFundsLoaded)
        {
            _lastTradeIndex = -1;
            _lastTradeOrderIndex = -1;
            pnlTradeControl.Controls.Clear();
            ddlFund.Items.Clear();
            lstTradeOrders.Items.Clear();
            lstTrades.Items.Clear();
            _viewModel.LoadFunds(onFundsLoaded);
        }

        void ClearTradeOrderControl()
        {
            var fundId = _viewModel.Funds.ElementAt(ddlFund.SelectedIndex).FundId;
            var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedItems[0].Index);
            var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0);
            pnlTradeControl.Draw(() =>
            {
                var tradeControl = default(Control);
                pnlTradeControl.Controls.Clear();
                var tradeType = fundOrderTrade.TradeType;
                switch (tradeType)
                {
                   case TradeType.ShortIronCondor:
                   case TradeType.LongIronCondor:
                       var valueDate = _viewModel.ValueDate.HasValue ? _viewModel.ValueDate.Value : DateTime.Now.Date;
                       var orderActionType = GetOrderActionType(fundOrderTrade.TradeState);
                       var baseContract = _viewModel.BaseContracts.Where(e => e.Symbol == fundOrderTrade.BaseContractSymbol).FirstOrDefault();
                       baseContract = baseContract ?? _viewModel.BaseContracts.ElementAt(0);
                       var viewModel = new IronCondorTradeOrderReadModel(this, _appRoot, valueDate, fundId, baseContract, fundOrder, fundOrderTrade, orderActionType, 
                           maturityDate => this.Post(() => txtDaysToExpiry.Text = $"{(maturityDate - dtpTradeDate.Value).Days}"),
                           tradeDate => this.Post(() => dtpTradeDate.Value = tradeDate));
                       tradeControl = new IronCondorTradeOrderView(this, viewModel);
                       break;
                }
                if (tradeControl != null)
                {
                    tradeControl.Dock = DockStyle.Fill;
                    pnlTradeControl.Controls.Add(tradeControl);
                }
                if (lstTradeOrders.SelectedIndices.Count > 0)
                {
                    btnAddTrade.Enabled = true;
                    btnRemoveTrade.Enabled = false;
                }
                _viewModel.OnShowButtons();
            
            });
            return;

            OrderActionType GetOrderActionType(TradeState tradeState) => tradeState == TradeState.TradeToClose ? OrderActionType.Close : OrderActionType.Open;
        }


        void EnableTradeButtons()
        {
            var enabled = lstTrades.SelectedIndices.Count > 0;
            btnRemoveTrade.Enabled = enabled;
        }

        void btnLoadOrder_Click(object sender, EventArgs e) => LoadTradeOrder();

        void LoadTradeOrder()
        {
            if (lstTradeOrders.SelectedIndices.Count > 0 && lstTrades.SelectedIndices.Count > 0)
            {
                var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
                switch (fundOrderTrade.TradeState)
                {
                    case TradeState.TradeToOpen:
                    case TradeState.TradeToClose:
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                        break;
                    case TradeState.OrderFilled:
                        var fundId = _viewModel.Funds.ElementAt(ddlFund.SelectedIndex).FundId;
                        var fundOrderTradeId = new FundOrderTradeId(fundId, fundOrderTrade.OrderId, fundOrderTrade.TradeId);
                        _viewModel.ChangeFundOrderTradeState(fundOrderTradeId, TradeState.TradeToOpen);
                        break;
                    default:
                        this.ShowErrorMessage($"Unable to load Trade Order {fundOrderTrade.OrderId}:{fundOrderTrade.TradeId} with Trade State: {fundOrderTrade.TradeState} ", "Load Trade Order Error");
                        this.DialogResult = DialogResult.Cancel;
                        this.Close();
                        break;
                }
            }
        }

        void ddlFund_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ddlFund.SelectedIndex < 0) return;
            _viewModel.FundSelectedIndex = ddlFund.SelectedIndex;
            if (lstTradeOrders.SelectedIndices.Count > 0)
                _viewModel.FundOrderSelectedIndex = lstTradeOrders.SelectedIndices[0];
            ShowFundOrders();
        }

        void ShowFundOrders()
        {
            _viewModel.FundOrders.Clear(); 
            lstTradeOrders.Items.Clear();
            lstTrades.Items.Clear();
            if (ddlFund.SelectedIndex >= 0)
            {
                var fromDate = dtpFrom.Value.AddMonths(-1);
                foreach (var fundOrder in _viewModel.Funds.ElementAt(_viewModel.FundSelectedIndex).Orders.Where(o => o.OrderDate >= fromDate && o.OrderDate <= dtpTo.Value))
                {
                    var fundOrderItem = new ListViewItem(new string[] {
                        $"{fundOrder.OrderId}",
                        $"{fundOrder.OrderDate:yyyy-MMM-dd}",
                        $"{fundOrder.OrderStatus}",
                        fundOrder.Reference ?? string.Empty
                    });
                    lstTradeOrders.Items.Add(fundOrderItem);
                    _viewModel.FundOrders.Add(fundOrder);
                }
                if (lstTradeOrders.Items.Count > 0)
                {
                    var index = _lastTradeOrderIndex != -1 ? _lastTradeOrderIndex : 0;
                    _lastTradeOrderIndex = -1;
                    lstTradeOrders.Items[index].Selected = true;
                    btnDeleteOrder.Enabled = true;
                    btnAddTrade.Enabled = true;
                }
                _viewModel.OnShowButtons();
             }
        }

        void lstTradeOrders_SelectedIndexChanged(object sender, EventArgs e)
        {
            _viewModel.FundOrderTrades.Clear();
            pnlTradeControl.Controls.Clear();
            ddlOrderActionType.Enabled = false;
            txtDaysToExpiry.Visible = false;
            lblDaysToExpiry.Visible = false;
            lstTrades.Items.Clear();
            if (lstTradeOrders.SelectedItems.Count > 0)
            {
                _viewModel.FundOrderSelectedIndex = lstTradeOrders.SelectedIndices[0];
                var fundOrderItem = lstTradeOrders.SelectedItems[0];
                var fund = _viewModel.GetFund(ddlFund.SelectedIndex);
                var fromDate = dtpFrom.Value.AddMonths(-1);
                var fundOrder = _viewModel.GetFundOrder(fund.FundId, fromDate, dtpTo.Value, fundOrderItem.Index);
                if (fundOrder != null)
                {
                    foreach (var fundOrderTrade in fundOrder.Trades)
                    {
                        var fundOrderTradeItem = new ListViewItem(new string[] {
                            $"{fundOrderTrade.TradeId}",
                            $"{fundOrderTrade.TradeType}",
                            $"{fundOrderTrade.TradeDate:yyyy-MMM-dd}",
                            $"{fundOrderTrade.MaturityDate:yyyy-MMM-dd}",
                            $"{fundOrderTrade.TradeState}",
                            $"{fundOrderTrade.TradeAction} {fundOrderTrade.Reference}"
                        });
                        lstTrades.Items.Add(fundOrderTradeItem);
                        _viewModel.FundOrderTrades.Add(fundOrderTrade);
                    }
                }
                if (lstTrades.Items.Count > 0)
                {
                    var index = _lastTradeIndex > -1 ? _lastTradeIndex : 0;
                    _lastTradeIndex = -1;
                    if (index < lstTrades.Items.Count && !lstTrades.Items[index].Selected)
                        lstTrades.Items[index].Selected = true;
                    else
                        lstTrades.Items[0].Selected = true;
                }
                _viewModel.OnShowButtons();
            }
        }

    
        void lstTrades_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_viewModel.FundOrders.Count > 0 && _viewModel.FundOrderTrades.Count > 0)
            {
                var index = lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0;
                if (_lastTradeIndex == index)
                    return;
                _lastTradeIndex = index;
                var fundOrderTrade = _viewModel.GetFundOrderTrade(index);
                var controls = new Control[] { dtpTradeDate, ddlOrderActionType };
                foreach (var o in controls)
                    o.Enabled = fundOrderTrade.TradeState == TradeState.NewTrade;
                txtTradeType.Text = fundOrderTrade.TradeType.ToString();
                dtpTradeDate.Value = fundOrderTrade.TradeDate;
                txtDaysToExpiry.Visible = true;
                lblDaysToExpiry.Visible = true;
                ClearTradeOrderControl();
            }
            return;
            
        }

        void btnCreateOrder_Click(object sender, EventArgs e)
        {
            var fundId = _viewModel.GetFundId(ddlFund.SelectedIndex);
            var valueDate = _viewModel.ValueDate.HasValue ? _viewModel.ValueDate.Value : DateTime.Now.Date;
            var vm = new FundOrderEditorViewModel(_appRoot, valueDate, _viewModel.BaseContracts, fundId);
            var dlg = new CreateFundOrderForm();
            dlg.SetViewModel(vm);
            if (dlg.ShowDialog() == DialogResult.OK)
                _viewModel.AddOrderToFund(dlg.FundOrder);
        }

        private void ddlLiveFeed_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        void lstTrades_Enter(object sender, EventArgs e)
            => EnableTradeButtons();

        void lstTrades_Leave(object sender, EventArgs e)
            => EnableTradeButtons();

        void btnAddTrade_Click(object sender, EventArgs e)
        {
            if (lstTradeOrders.SelectedIndices.Count > 0)
            {
                var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
                var dlg = new CreateFundOrderTradeForm(_appRoot);
                dlg.SetViewModel(_viewModel);
                dlg.SetFundOrder(fundOrder);
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var fundOrderTrade = dlg.FundOrderTrade with
                    {
                        FundId = fundOrder.FundId,
                        OrderId = fundOrder.OrderId,
                        TradeDate = fundOrder.TradeDate,
                        MaturityDate = fundOrder.MaturityDate,
                        PrimaryTrade = fundOrder.Trades.Count() == 0
                    };
                    var selectedIndex = ddlFund.SelectedIndex;
                    _lastTradeOrderIndex = lstTradeOrders.SelectedIndices[0];
                    _lastTradeIndex = lstTrades.Items.Count;
                    _viewModel.LoadNewTradeId(newTradeId => _viewModel.AddTradeToFundOrder(fundOrderTrade with { TradeId = newTradeId }));
                }
            }
        }

        void btnRemoveTrade_Click(object sender, EventArgs e)
        {
            var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
            if (lstTrades.SelectedIndices.Count > 0)
            {
                var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
                _viewModel.RemoveTradeFromFundOrder(fundOrderTrade.Id);
            }
        }

        void btnClearTrade_Click(object sender, EventArgs e) => ClearTradeOrderControl();

        void btnSubmitOrder_Click(object sender, EventArgs e)
        {
            var orderActionType = (OrderActionType)Enum.Parse(typeof(OrderActionType), ddlOrderActionType.SelectedItem.ToString());
            var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
            var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
            var orderConfirmation = new TradeOrderConfirmationViewModel(viewModel => {
                var confirmTradeOrder = true;
                var dlg = new TradeOrderConfirmationForm(viewModel);
                if (dlg.ShowDialog() == DialogResult.Cancel)
                    confirmTradeOrder = false;
                return confirmTradeOrder;
            });
            tradeOrderControl.SubmitOrder(dtpTradeDate.Value, orderActionType, orderConfirmation, _viewModel.SetCommandId );
        }

        void dtpTradeDate_ValueChanged(object sender, EventArgs e)
        {
            if (pnlTradeControl.Controls.Count > 0)
            {
                var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
                txtDaysToExpiry.Text = (tradeOrderControl.MaturityDate - dtpTradeDate.Value).Days.ToString();
            }
        }

        void btnCancelOrder_Click(object sender, EventArgs e)
        {
            if (lstTradeOrders.SelectedIndices.Count > 0)
            {
                var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedIndices[0]);
                var dlg = new DeleteFundOrderForm($"Are you sure you want to delete order:{Environment.NewLine} {fundOrder.OrderId} {fundOrder.Reference ?? string.Empty} ?");
                if (dlg.ShowDialog() == DialogResult.Yes)
                    _viewModel.RemoveOrderFromFund(fundOrder.Id);
            }
        }

        void btnNearestStrikes_Click(object sender, EventArgs e)
        {
            var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
            tradeOrderControl?.SetNearestStrikePrices();
        }

        void btnEndOfDay_Click(object sender, EventArgs e)
        {
            var index = lstTradeOrders.SelectedIndices.Count > 0 ? lstTradeOrders.SelectedIndices[0] : 0;
            var fundOrder = _viewModel.GetFundOrder(index);
            index = lstTrades.SelectedIndices.Count > 0 ? lstTrades.SelectedIndices[0] : 0;
            var fundOrderTrade = _viewModel.GetFundOrderTrade(lstTrades.SelectedIndices[0]);
            var baseContract = _viewModel.BaseContracts.Where(o => o.Symbol == fundOrderTrade.BaseContractSymbol.Trim()).FirstOrDefault();
            var eodParam = new TradeEndOfDayParameter
            {
                FundId = fundOrder.FundId,
                OrderId = fundOrder.OrderId,
                TradeId = fundOrderTrade.TradeId,
                TradeType = fundOrderTrade.TradeType,
                BaseContractId =baseContract.ContractId,
                ValueDate = dtpTradeDate.Value
            };
            var dlg = new TradeEndOfDayForm(_appRoot, eodParam);
            var dlgResult = dlg.ShowDialog();
            if (dlgResult == DialogResult.OK)
                LoadFunds();
        }

        void btnCreateFund_Click(object sender, EventArgs e)
        {
            var vm = new CreateFundReadModel(_appRoot);
            var dlg = new CreateFundForm(vm);
            switch (dlg.ShowDialog())
            {
                case DialogResult.OK:
                    _viewModel.SetSelectedFundIndex(dlg.Fund.FundId);
                    LoadFunds();
                    break;
            }
        }

        void ddlOrderActionType_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (pnlTradeControl.Controls.Count == 0) return;
            var orderActionType = (OrderActionType)Enum.Parse(typeof(OrderActionType), ddlOrderActionType.SelectedItem.ToString());
            _viewModel.OrderActionType = orderActionType;   
            var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
            tradeOrderControl?.OrderActionTypeChanged(orderActionType);
        }

        void dtpFrom_ValueChanged(object sender, EventArgs e)
        {
            if (!dtpFrom.Enabled) return;
            dtpTo.Value = new DateTime(dtpFrom.Value.Year, dtpFrom.Value.Month, DateTime.DaysInMonth(dtpFrom.Value.Year, dtpFrom.Value.Month), 23,59,59);
        }

        void dtpTo_ValueChanged(object sender, EventArgs e)
        {
            if (!dtpTo.Enabled) return;
            LoadFunds();
        }

        void btnCloseOrder_Click(object sender, EventArgs e)
        {
            if (lstTradeOrders.SelectedItems.Count > 0)
            {
                var fundOrder = _viewModel.GetFundOrder(lstTradeOrders.SelectedItems[0].Index);
                _viewModel.CloseFundOrder(fundOrder.Id);
            }
        }

        void lstTradeOrders_DoubleClick(object sender, EventArgs e) => LoadTradeOrder();

        void pnlTradePosition_Paint(object sender, PaintEventArgs e)
        {

        }

        void TradeOrderEditorForm_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        public void Open()
        {
            throw new NotImplementedException();
        }

        void IFormControl.Resize(Control parentControl)
        {
            throw new NotImplementedException();
        }

        private void cbLiveFeed_CheckedChanged(object sender, EventArgs e)
        {
            cbLiveFeed.BackColor = cbLiveFeed.Checked switch
            {
                _ when !cbLiveFeed.Enabled => Color.DarkGray,
                _ when cbLiveFeed.Checked => Color.LimeGreen,
                _ => Color.Red, 
            };

            var tradeOrderControl = pnlTradeControl.Controls[0] as ITradeOrderControl;
            tradeOrderControl.LiveFeed(cbLiveFeed.Checked);
        }
    }
}
