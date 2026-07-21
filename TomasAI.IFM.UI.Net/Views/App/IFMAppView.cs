using System;
using System.Reflection;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Configuration;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.UI.Net.Views.SystemAdmin;
using TomasAI.IFM.UI.Net.Views.MarketData;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.Views.Fund;
using TomasAI.IFM.UI.Net.Views.Reference;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.App;

public partial class IFMAppView : Form, IForm<IFMAppView>, IFormControl
{
    private IAppRoot _appRoot;
    private Control? _tradeBlotter;
    private IFMAppViewModel _viewModel = null!;
    private Dictionary<ActionState, Color> _tradePlanStateMap = null!;
    private Version _appVersion;

    public IFMAppView(IAppRoot appRoot)
    {
        _appRoot = appRoot;
        InitializeComponent();
        _appVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        this.Text += $" - v{_appVersion} - {appRoot.AppEnvironment}";
    }

    private void IFMApp_Load(object sender, EventArgs e)
    {
        _viewModel = new IFMAppViewModel(_appRoot);
        _viewModel.AppStartup(
            appVersion: _appVersion,
            appEnvironment: _appRoot.AppEnvironment,
            onErrorMessage: (errorMessage, caption) => this.ShowErrorMessage(errorMessage, caption),
            onEnableMenuBarButtons: () => this.Post(() => {
                tradeButton.Enabled = true;
                marketDataButton.Enabled = true;
                fundButton.Enabled = true;
                referenceButton.Enabled = true;
                systemAdminButton.Enabled = true;
            }),
            loadStatusConsole: (contractId, valueDate) => this.Post(() => statusConsoleView1.LoadView(_appRoot, contractId, valueDate)),
            unloadStatusConsole: () => this.Post(() => statusConsoleView1.UnloadView()),
            writeStatusLine: statusMessage => this.Post(() =>  lblStatus.Text = statusMessage),
            writeStatusConsole: logItems => this.Post(() => {
                statusConsoleView1.RefreshStatusConsole(logItems);
            }),
            updateMarketOutlook: futuresEodData => this.Post(() => marketOutlookView1.RefreshView(futuresEodData)),
            updateTradeSignal: futuresTradeSignal => this.Post(() => marketOutlookView1.RefreshView(futuresTradeSignal)),
            notifyTradePlacement: placeTrade => this.Post(() => marketOutlookView1.RefreshView(placeTrade)),
            updateMarketData: (symbol, futuresBarData) => this.Post(() => marketDataView1.RefreshView(symbol, futuresBarData)),
            closeTradeBlotters: () => this.Post(() => CloseTradeBlotters())
        );

        _tradePlanStateMap = new Dictionary<ActionState, Color> {
            { ActionState.Normal, Color.LimeGreen },
            { ActionState.Warning, Color.Yellow },
            { ActionState.Critical, Color.Orange },
            { ActionState.RedAlert, Color.Red },
        };
        //lstStatusConsole.SetDoubleBuffered(true);
     }

    private void IFMApp_FormClosing(object sender, FormClosingEventArgs e)
    {
        _viewModel.AppShutdown();
    }

    private void tradeButton_Click(object sender, EventArgs e)
    {
        var dlg = _appRoot.GetForm<TradeOrderEditorForm>();
        dlg.LoadViewModel(new TradeOrderEditorViewModel(_appRoot, _viewModel.ValueDate, _viewModel.BaseContracts));
        switch (dlg.ShowDialog())
        {
            case DialogResult.OK:
                if (dlg.FundOrderTrade is not null)
                {
                    var tabPageName = $"{dlg.FundOrderTrade.OrderId}:{dlg.FundOrderTrade.TradeId}";
                    tabTradeBlotter.TabPages.Add(tabPageName);
                    for (var index = 0; index < tabTradeBlotter.TabPages.Count; index++)
                        if (tabTradeBlotter.TabPages[index].Text == tabPageName)
                        {
                            var tabPage = tabTradeBlotter.TabPages[index];
                            _tradeBlotter = TradeBlotterFactory.Create(tabPage, _appRoot, dlg.Fund, dlg.FundOrder, dlg.FundOrderTrade, _viewModel.ValueDate, _viewModel.BaseContracts);
                            if (_tradeBlotter is not null)
                                 ((IFormControl)_tradeBlotter)?.Open();
                            tabPage.BackColor = SystemColors.ControlDarkDark;
                            tabPage.Controls.Clear();
                            tabPage.Controls.Add(_tradeBlotter);
                            break;
                        }
                }
                break;
            default:
                break;
        }
        if (dlg.FundOrder != null)
        {
            if (tabTradeBlotter.TabPages.Count > 0)
            {
                btnCloseOrder.Text = $"Close Order: {tabTradeBlotter.SelectedTab!.Text}";
                btnCloseOrder.Visible = true;
                ResizeTabPages();
            }
            else
            {
                btnCloseOrder.Visible = false;
            }
        }
    }

    private void marketDataButton_Click(object sender, EventArgs e)
    {
        var dlg = _appRoot.GetForm<MarketDataForm>();
        dlg.LoadViewModel(new MarketDataViewModel(_appRoot));
        dlg.ShowDialog();
    }

    private void fundButton_Click(object sender, EventArgs e)
    {
        var dlg = _appRoot.GetForm<FundTransactionEditor>();
        dlg.LoadViewModel(new FundTransactionEditorViewModel(_appRoot));
        dlg.ShowDialog();
    }

    private void referenceButton_Click(object sender, EventArgs e)
    {
        var dlg = _appRoot.GetForm<ReferenceForm>();
        dlg.LoadViewModel(new ReferenceViewModel(_appRoot));
        dlg.ShowDialog();
    }

    private void systemAdminButton_Click(object sender, EventArgs e)
    {
        var dlg = _appRoot.GetForm<SystemAdminForm>();
        dlg.LoadViewModel(new SystemAdminViewModel(_appRoot));
        dlg.ShowDialog();
    }

    private void IFMApp_Resize(object sender, EventArgs e)
    {
        ResizeTabPages();
        marketOutlookView1.ResizeView(pnlMarketOutlook);
        statusConsoleView1.ResizeView(pnlStatusConsole);
    }

    private void btnCloseOrder_Click(object sender, EventArgs e)
    {
        if (_tradeBlotter != null)
            ((IFormControl)_tradeBlotter).Close();
         var tabPage = tabTradeBlotter.SelectedTab!;
        tabPage.Controls.Clear();
        tabTradeBlotter.TabPages.Remove(tabPage);
        if (tabTradeBlotter.TabPages.Count == 0)
            btnCloseOrder.Visible = false;
    }

    private void tradeSplitter_SplitterMoved(object sender, SplitterEventArgs e)
        => ResizeTabPages();


    private void ResizeTabPages()
    {
        foreach (TabPage tabPage in tabTradeBlotter.TabPages)
            foreach (Control control in tabPage.Controls)
            {
                if (control is IFormControl)
                    ((IFormControl)control).Resize(tabPage);
            }
    }

    private void CloseTradeBlotters()
    {
        foreach (TabPage tabPage in tabTradeBlotter.TabPages)
        {
            foreach (Control control in tabPage.Controls)
            {
                if (control is IFormControl)
                    ((IFormControl)control).Close();
            }
            tabPage.Controls.Clear();
            tabTradeBlotter.TabPages.Remove(tabPage);
            if (tabTradeBlotter.TabPages.Count == 0)
                btnCloseOrder.Visible = false;
        }
    }

    private void gridStatusConsole_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {

    }

    private void statusConsoleLogViewModelBindingSource_CurrentChanged(object sender, EventArgs e)
    {

    }

    private void economicCalendarView1_Load(object sender, EventArgs e)
    {
        economicCalendarView1.LoadView(_appRoot);
    }

    private void tabTradeBlotter_SelectedIndexChanged(object sender, EventArgs e)
    {

    }

    private void marketDataView1_Load(object sender, EventArgs e)
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
}
