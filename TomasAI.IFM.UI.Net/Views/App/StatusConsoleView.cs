using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Reference;

namespace TomasAI.IFM.UI.Net.Views.App;

public partial class StatusConsoleView : UserControl
{
    StatusConsoleViewModel? _viewModel;

    public StatusConsoleView()
    {
        InitializeComponent();
        pnlTradeLayout.Height = 25;
        txtTradeStatus.Enabled = false;
        lstStatusConsole.SetDoubleBuffered(true);
    }

    public void LoadView(IAppRoot appRoot, string contractId, DateOnly valueDate)
    {
        _viewModel = new (appRoot, contractId, valueDate)
        {
            // set view model action events...
            OnTradeStatusLoad = e => this.Post(() => RefreshTradeConsole(e)),
            OnTradeStatusChanged = e => this.Post(() => RefreshTradeStatus(e)),
            OnMDIForwardLossRatiosLoaded = e => this.Post(() => RefreshMDIForwardLossRatios(e))
        };
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
        lstStatusConsole.Items.Insert(0, new ListViewItem([
                        $"{logItems[0].StatusDate:T}",
                        logItems[0].Message
                    ]));
        lstStatusConsole.EndUpdate();
    }

    public void RefreshTradeConsole(FuturesItiSignalV2ReadModel[] futuresItiSignals)
    {
        lstTradeStatus.BeginUpdate();
        lstTradeStatus.Items.Clear();
        lstTradeStatus.Items.AddRange([.. futuresItiSignals.Select( SetTradeStatusListViewItem )]);
        lstTradeStatus.EndUpdate();
        RefreshTradeStatus( _viewModel!.GetTradeStatus());
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
        Width = parentControl.Width;
        Height = parentControl.Height;
    }

    ListViewItem SetTradeStatusListViewItem(FuturesItiSignalV2ReadModel e)
        =>  new ([
                        $"{e.IntrinsicTime:T}",
                        $"{e.ContractId} - {e.IntrinsicTimeTrend} @ {e.IntrinsicPrice:F2} := {e.TargetDelta:F4}"]);

    ListViewItem SetTradeStatusListViewItem(MDIForwardLossRatioUIViewModel e)
        => new ([
                        e.MDI,
                        e.TrendDirection,
                        e.TradeType,
                        e.ForwardLossRatio]);

    private void tabConsoles_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (tabConsoles.SelectedIndex == 0)
        {
            _viewModel?.LoadTradeStatus();
        }
    }
}
