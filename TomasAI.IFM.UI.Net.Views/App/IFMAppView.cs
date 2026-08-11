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
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Extensions;
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

public partial class IFMAppView : Form, IForm<IFMAppView>, IFormControl, IIFMAppLiveViewAdapter
{
    private IAppRoot _appRoot;
    private readonly IViewNavigator _navigator;
    private Control? _tradeBlotter;
    private IFMAppViewModel _viewModel = null!;
    private Dictionary<ActionState, Color> _tradePlanStateMap = null!;
    private Version _appVersion;
    private bool _shutdownComplete;
    private long _lastErrorSequence;

    public IFMAppView(IAppRoot appRoot, IViewNavigator navigator)
    {
        _appRoot = appRoot;
        _navigator = navigator;
        InitializeComponent();
        _appVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        this.Text += $" - v{_appVersion} - {appRoot.AppEnvironment}";
    }

    private async void IFMApp_Load(object sender, EventArgs e)
    {
        _viewModel = new IFMAppViewModel(
            _appRoot,
            _appVersion,
            _appRoot.AppEnvironment,
            this);
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        RenderShellState();
        try
        {
            await _viewModel.StartupOperation.ExecuteAsync();
            RenderShellState();
        }
        catch (Exception ex)
        {
            this.ShowErrorMessage(ex.Message, "Application Startup Error");
            return;
        }

        _tradePlanStateMap = new Dictionary<ActionState, Color> {
            { ActionState.Normal, Color.LimeGreen },
            { ActionState.Warning, Color.Yellow },
            { ActionState.Critical, Color.Orange },
            { ActionState.RedAlert, Color.Red },
        };
        //lstStatusConsole.SetDoubleBuffered(true);
     }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
        => this.Post(() =>
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(IFMAppViewModel.IsMenuEnabled):
                    RenderMenuState();
                    break;
                case nameof(IFMAppViewModel.StatusLine):
                    lblStatus.Text = _viewModel.StatusLine;
                    break;
                case nameof(IFMAppViewModel.LatestStatusLog):
                    if (_viewModel.LatestStatusLog is { } statusLog)
                        statusConsoleView1.AppendStatusConsole(statusLog);
                    break;
                case nameof(IFMAppViewModel.StatusConsole):
                    if (_viewModel.StatusConsole is { } statusConsole)
                        statusConsoleView1.LoadViewModel(statusConsole);
                    break;
                case nameof(IFMAppViewModel.LastError):
                    RenderLatestError();
                    break;
                case nameof(IFMAppViewModel.IsCloseRequested):
                    if (_viewModel.IsCloseRequested)
                        Close();
                    break;
            }
        });

    private void RenderShellState()
    {
        RenderMenuState();
        lblStatus.Text = _viewModel.StatusLine;
        statusConsoleView1.RenderStatusConsole(_viewModel.StatusLogs);
        if (_viewModel.StatusConsole is { } statusConsole)
            statusConsoleView1.LoadViewModel(statusConsole);
        RenderLatestError();
    }

    private void RenderMenuState()
    {
        tradeButton.Enabled = _viewModel.IsMenuEnabled;
        marketDataButton.Enabled = _viewModel.IsMenuEnabled;
        fundButton.Enabled = _viewModel.IsMenuEnabled;
        referenceButton.Enabled = _viewModel.IsMenuEnabled;
        systemAdminButton.Enabled = _viewModel.IsMenuEnabled;
    }

    private void RenderLatestError()
    {
        if (_viewModel.LastError is not { } error || error.Sequence <= _lastErrorSequence)
            return;

        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    /// <inheritdoc />
    public void UpdateMarketOutlook(FuturesEodDataUIViewModel futuresEodData)
        => this.Post(() => marketOutlookView1.RefreshView(futuresEodData));

    /// <inheritdoc />
    public void UpdateTradeSignal(FuturesTradeSignalUIViewModel futuresTradeSignal)
        => this.Post(() => marketOutlookView1.RefreshView(futuresTradeSignal));

    /// <inheritdoc />
    public void NotifyTradePlacement(PlaceTradeUIViewModel placeTrade)
        => this.Post(() => marketOutlookView1.RefreshView(placeTrade));

    /// <inheritdoc />
    public void UpdateMarketData(string symbol, FuturesBarDataReadModel[] futuresBarData)
        => this.Post(() => marketDataView1.RefreshView(symbol, futuresBarData));

    private async void IFMApp_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_shutdownComplete)
            return;

        e.Cancel = true;
        try
        {
            await ((IAsyncFormControl)economicCalendarView1).CloseAsync();
            await _viewModel.ShutdownOperation.ExecuteAsync();
            statusConsoleView1.UnloadView();
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
            await _viewModel.DisposeAsync();
            _shutdownComplete = true;
            Close();
        }
        catch (Exception ex)
        {
            this.ShowErrorMessage(ex.Message, "Application Shutdown Error");
        }
    }

    private void tradeButton_Click(object sender, EventArgs e)
    {
        TradeOrderEditorForm? dlg = null;
        var navigationResult = _navigator.ShowModal<TradeOrderEditorForm>(view =>
        {
            dlg = view;
            view.LoadViewModel(new TradeOrderEditorViewModel(
                _appRoot,
                _viewModel.ValueDate,
                [.. _viewModel.BaseContracts]));
        });
        switch (navigationResult)
        {
            case NavigationResult.Accepted:
                if (dlg.FundOrderTrade is not null)
                {
                    var tabPageName = $"{dlg.FundOrderTrade.OrderId}:{dlg.FundOrderTrade.TradeId}";
                    tabTradeBlotter.TabPages.Add(tabPageName);
                    for (var index = 0; index < tabTradeBlotter.TabPages.Count; index++)
                        if (tabTradeBlotter.TabPages[index].Text == tabPageName)
                        {
                            var tabPage = tabTradeBlotter.TabPages[index];
                            _tradeBlotter = TradeBlotterFactory.Create(
                                tabPage,
                                _appRoot,
                                dlg.Fund,
                                dlg.FundOrder,
                                dlg.FundOrderTrade,
                                _viewModel.ValueDate,
                                [.. _viewModel.BaseContracts]);
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
        if (dlg?.FundOrder != null)
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
        _navigator.ShowModal<MarketDataForm>(view =>
            view.LoadViewModel(new MarketDataViewModel(_appRoot)));
    }

    private void fundButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<FundTransactionEditor>(view =>
            view.LoadViewModel(new FundTransactionEditorViewModel(_appRoot)));
    }

    private void referenceButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<ReferenceForm>(view =>
            view.LoadViewModel(new ReferenceViewModel(_appRoot)));
    }

    private void systemAdminButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<SystemAdminForm>(view =>
            view.LoadViewModel(new SystemAdminViewModel(_appRoot)));
    }

    private void IFMApp_Resize(object sender, EventArgs e)
    {
        ResizeTabPages();
        marketOutlookView1.ResizeView(pnlMarketOutlook);
        statusConsoleView1.ResizeView(pnlStatusConsole);
    }

    private async void btnCloseOrder_Click(object sender, EventArgs e)
    {
        if (_tradeBlotter != null)
            await CloseControlAsync((IFormControl)_tradeBlotter);
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

    /// <inheritdoc />
    public async ValueTask CloseTradeBlottersAsync()
    {
        for (var tabIndex = tabTradeBlotter.TabPages.Count - 1; tabIndex >= 0; tabIndex--)
        {
            var tabPage = tabTradeBlotter.TabPages[tabIndex];
            foreach (Control control in tabPage.Controls)
            {
                if (control is IFormControl)
                    await CloseControlAsync((IFormControl)control);
            }
            tabPage.Controls.Clear();
            tabTradeBlotter.TabPages.RemoveAt(tabIndex);
            if (tabTradeBlotter.TabPages.Count == 0)
                btnCloseOrder.Visible = false;
        }
    }

    static async ValueTask CloseControlAsync(IFormControl control)
    {
        if (control is IAsyncFormControl asyncControl)
            await asyncControl.CloseAsync();
        else
            control.Close();
    }

    private void gridStatusConsole_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {

    }

    private void statusConsoleLogViewModelBindingSource_CurrentChanged(object sender, EventArgs e)
    {

    }

    private async void economicCalendarView1_Load(object sender, EventArgs e)
    {
        await economicCalendarView1.LoadViewAsync(_appRoot);
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
