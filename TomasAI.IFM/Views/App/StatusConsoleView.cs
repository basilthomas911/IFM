using System;
using System.Windows.Forms;
using System.Linq;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Extensions;
using TomasAI.IFM.Contracts;
using TomasAI.IFM.ViewModels.App;
using TomasAI.IFM.ViewModels.MarketData;
using TomasAI.IFM.ViewModels.Reference;

namespace TomasAI.IFM.Views.App
{
    public partial class StatusConsoleView : UserControl
    {
        StatusConsoleViewModel _viewModel;

        public StatusConsoleView()
        {
            InitializeComponent();
            pnlTradeLayout.Height = 25;
            txtTradeStatus.Enabled = false;
            lstStatusConsole.SetDoubleBuffered(true);
        }

        public void LoadView(IAppRoot appRoot, string contractId, DateTime valueDate)
        {
            _viewModel = new StatusConsoleViewModel(appRoot, contractId, valueDate);

            // set view model action events...
            _viewModel.OnTradeStatusLoad = e => this.Post(() => RefreshTradeConsole(e));
            _viewModel.OnTradeStatusChanged = e => this.Post(() => RefreshTradeStatus(e));
            _viewModel.OnMDIForwardLossRatiosLoaded = e => this.Post(() => RefreshMDIForwardLossRatios(e));
            _viewModel.StartMarketDataAnalyticsEventConsumer();
            _viewModel.LoadTradeStatus();
            _viewModel.LoadMDIForwardLossRatios();
        }

        public void UnloadView()
        {
            _viewModel?.StopMarketDataAnalyticsEventConsumer();
        }

        public void RefreshStatusConsole(StatusConsoleLogReadModel[] logItems)
        {
            lstStatusConsole.BeginUpdate();
            lstStatusConsole.Items.Insert(0, new ListViewItem(new string[] {
                            $"{logItems[0].StatusDate:T}",
                            logItems[0].Message
                        }));
            lstStatusConsole.EndUpdate();
        }

        public void RefreshTradeConsole(FuturesItiSignalViewModel[] futuresItiSignals)
        {
            lstTradeStatus.BeginUpdate();
            lstTradeStatus.Items.Clear();
            lstTradeStatus.Items.AddRange(futuresItiSignals.Select( SetTradeStatusListViewItem ).ToArray());
            lstTradeStatus.EndUpdate();
            RefreshTradeStatus( _viewModel.GetTradeStatus());
        }

        public void RefreshTradeStatus(FuturesTradeStatusUIViewModel e)
        {
            txtTradeStatus.Text = e.TradeStatus;
            txtTradeStatus.ForeColor = e.TradeStatusForeColor;
            txtTradeStatus.BackColor = e.TradeStatusBackColor;
            txtTradeStatus.Enabled = true;
        }

        public void RefreshMDIForwardLossRatios(MDIForwardLossRatioUIViewModel[] mdiForwardLossRatios)
        {
            lstMDIFwdLossRatio.BeginUpdate();
            lstMDIFwdLossRatio.Items.Clear();
            lstMDIFwdLossRatio.Items.AddRange(mdiForwardLossRatios.Select(SetTradeStatusListViewItem).ToArray());
            lstMDIFwdLossRatio.EndUpdate();
        }

        public void ResizeView(Control parentControl)
        {
            this.Width = parentControl.Width;
            this.Height = parentControl.Height;
        }

        ListViewItem SetTradeStatusListViewItem(FuturesItiSignalViewModel e)
            =>  new ListViewItem(new string[] {
                            $"{e.IntrinsicTime:T}",
                            $"{e.ContractId} - {e.IntrinsicTimeTrend} @ {e.IntrinsicPrice:F2} := {e.TargetDelta:F4}"});

        ListViewItem SetTradeStatusListViewItem(MDIForwardLossRatioUIViewModel e)
            => new ListViewItem(new string[] {
                            e.MDI,
                            e.TrendDirection,
                            e.TradeType,
                            e.ForwardLossRatio
                        });

        private void tabConsoles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabConsoles.SelectedIndex == 0)
            {
                _viewModel?.LoadTradeStatus();
            }
        }
    }
}
