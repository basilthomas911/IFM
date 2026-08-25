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
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Fund;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Views.App;

public partial class IFMAppView : Form, IForm<IFMAppView>, IFormControl, IIFMAppLiveViewAdapter
{
    static readonly TimeSpan PresentationShutdownTimeout = TimeSpan.FromSeconds(10);
    const double DefaultSidePanelWidthRatio = 0.22;
    const int DwmUseImmersiveDarkMode = 20;
    const int DwmUseImmersiveDarkModeBefore20H1 = 19;
    const int DwmCaptionColor = 35;
    const int DwmTextColor = 36;
    private IAppRoot _appRoot;
    private readonly IViewNavigator _navigator;
    private readonly IEconomicCalendarService _economicCalendarService;
    private readonly IReferenceDataService _referenceDataService;
    private Control? _tradeBlotter;
    private IFMAppViewModel _viewModel = null!;
    private Dictionary<ActionState, Color> _tradePlanStateMap = null!;
    private Version _appVersion;
    private bool _shutdownStarted;
    private bool _shutdownComplete;
    private long _lastErrorSequence;
    private int _statusLogsRenderPending;

    public IFMAppView(
        IAppRoot appRoot,
        IViewNavigator navigator,
        IReferenceDataService referenceDataService,
        IEconomicCalendarService economicCalendarService)
    {
        _appRoot = appRoot;
        _navigator = navigator;
        _referenceDataService = referenceDataService;
        _economicCalendarService = economicCalendarService;
        InitializeComponent();
        operationViewSplitter.Paint += DashboardSplitter_Paint;
        marketViewSplitter.Paint += DashboardSplitter_Paint;
        _appVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        this.Text += $" - v{_appVersion} - {appRoot.AppEnvironment}";
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        var enabled = 1;
        if (DwmSetWindowAttribute(
                Handle,
                DwmUseImmersiveDarkMode,
                ref enabled,
                sizeof(int)) < 0)
        {
            _ = DwmSetWindowAttribute(
                Handle,
                DwmUseImmersiveDarkModeBefore20H1,
                ref enabled,
                sizeof(int));
        }

        var black = ColorTranslator.ToWin32(Color.Black);
        _ = DwmSetWindowAttribute(
            Handle,
            DwmCaptionColor,
            ref black,
            sizeof(int));

        var white = ColorTranslator.ToWin32(Color.White);
        _ = DwmSetWindowAttribute(
            Handle,
            DwmTextColor,
            ref white,
            sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    private async void IFMApp_Load(object sender, EventArgs e)
    {
        InitializeDashboardSplitters();
        _viewModel = new IFMAppViewModel(
            _appRoot,
            _appVersion,
            _appRoot.AppEnvironment,
            this,
            _economicCalendarService);
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
    {
        if (_shutdownStarted || eventArgs.PropertyName == nameof(IFMAppViewModel.UiDispatchMetrics))
            return;

        if (eventArgs.PropertyName == nameof(IFMAppViewModel.StatusLogs))
        {
            QueueStatusLogsRender();
            return;
        }

        var postedAt = Stopwatch.GetTimestamp();
        this.Post(() =>
        {
            if (_shutdownStarted)
                return;
            var renderStarted = Stopwatch.GetTimestamp();
            try
            {
                RenderProperty(eventArgs.PropertyName);
            }
            finally
            {
                _viewModel.RecordUiDispatch(
                    Stopwatch.GetElapsedTime(postedAt, renderStarted),
                    Stopwatch.GetElapsedTime(renderStarted));
            }
        });
    }

    void QueueStatusLogsRender()
    {
        if (Interlocked.Exchange(ref _statusLogsRenderPending, 1) != 0)
            return;

        var postedAt = Stopwatch.GetTimestamp();
        this.Post(() =>
        {
            // Reset before reading the latest snapshot. An update concurrent with rendering can
            // then enqueue one more pass, while all earlier notifications remain coalesced.
            Interlocked.Exchange(ref _statusLogsRenderPending, 0);
            if (_shutdownStarted)
                return;
            var renderStarted = Stopwatch.GetTimestamp();
            try
            {
                RenderProperty(nameof(IFMAppViewModel.StatusLogs));
            }
            finally
            {
                _viewModel.RecordUiDispatch(
                    Stopwatch.GetElapsedTime(postedAt, renderStarted),
                    Stopwatch.GetElapsedTime(renderStarted));
            }
        });
    }

    private void RenderProperty(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(IFMAppViewModel.IsMenuEnabled):
                RenderMenuState();
                break;
            case nameof(IFMAppViewModel.IsMarketDataFeedActive):
            case nameof(IFMAppViewModel.IsMarketDataFeedOperationInProgress):
            case nameof(IFMAppViewModel.CanToggleMarketDataFeed):
            case nameof(IFMAppViewModel.MarketDataFeedActionText):
            case nameof(IFMAppViewModel.MarketDataFeedStateText):
            case nameof(IFMAppViewModel.MarketDataFeedHealthIndicatorText):
            case nameof(IFMAppViewModel.MarketDataFeedHealthState):
                RenderMarketDataFeedState();
                break;
            case nameof(IFMAppViewModel.StatusLine):
                RenderStatusLine();
                break;
            case nameof(IFMAppViewModel.StatusLogs):
                statusConsoleView1.RenderStatusConsole(_viewModel.StatusLogs);
                break;
            case nameof(IFMAppViewModel.Operations):
                if (_viewModel.Operations is { } operations)
                    operationsView1.RefreshView(operations);
                break;
            case nameof(IFMAppViewModel.MarketOutlook):
                if (_viewModel.MarketOutlook is { } marketOutlook)
                    marketOutlookView1.RefreshView(marketOutlook);
                break;
            case nameof(IFMAppViewModel.FuturesTradeSignal):
                if (_viewModel.FuturesTradeSignal is { } tradeSignal)
                    marketOutlookView1.RefreshView(tradeSignal);
                break;
            case nameof(IFMAppViewModel.LatestTradePlacement):
                if (_viewModel.LatestTradePlacement is { } tradePlacement)
                    marketOutlookView1.RefreshView(tradePlacement);
                break;
            case nameof(IFMAppViewModel.LatestFuturesBarSnapshot):
                if (_viewModel.LatestFuturesBarSnapshot is { } futuresBars)
                    marketDataView1.RefreshView(futuresBars.Symbol, futuresBars.Bars);
                break;
            case nameof(IFMAppViewModel.LastError):
                RenderLatestError();
                break;
            case nameof(IFMAppViewModel.IsCloseRequested):
                if (_viewModel.IsCloseRequested)
                    Close();
                break;
        }
    }

    private void RenderShellState()
    {
        RenderMenuState();
        RenderStatusLine();
        statusConsoleView1.RenderStatusConsole(_viewModel.StatusLogs);
        if (_viewModel.Operations is { } operations)
            operationsView1.LoadViewModel(operations);
        if (_viewModel.MarketOutlook is { } marketOutlook)
            marketOutlookView1.RefreshView(marketOutlook);
        if (_viewModel.FuturesTradeSignal is { } tradeSignal)
            marketOutlookView1.RefreshView(tradeSignal);
        if (_viewModel.LatestTradePlacement is { } tradePlacement)
            marketOutlookView1.RefreshView(tradePlacement);
        foreach (var futuresBars in _viewModel.FuturesBarSnapshots)
            marketDataView1.RefreshView(futuresBars.Key, futuresBars.Value);
        RenderLatestError();
    }

    private void RenderMenuState()
    {
        tradeButton.Enabled = _viewModel.IsMenuEnabled;
        marketDataButton.Enabled = _viewModel.IsMenuEnabled;
        fundButton.Enabled = _viewModel.IsMenuEnabled;
        referenceButton.Enabled = _viewModel.IsMenuEnabled;
        systemAdminButton.Enabled = _viewModel.IsMenuEnabled;
        RenderMarketDataFeedState();
    }

    private void RenderMarketDataFeedState()
    {
        marketDataFeedButton.Text = _viewModel.MarketDataFeedActionText;
        marketDataFeedButton.AccessibleName = _viewModel.MarketDataFeedActionText;
        marketDataFeedButton.AccessibleDescription = _viewModel.MarketDataFeedStateText;
        marketDataFeedButton.ToolTipText = _viewModel.MarketDataFeedStateText;
        marketDataFeedButton.Enabled = _viewModel.CanToggleMarketDataFeed;
        (marketDataFeedButton.BackColor, marketDataFeedButton.ForeColor) =
            MarketDataFeedColors(_viewModel.IsMarketDataFeedActive);
        marketDataFeedHealthIndicator.Text = _viewModel.MarketDataFeedHealthIndicatorText;
        marketDataFeedHealthIndicator.AccessibleName = _viewModel.MarketDataFeedHealthIndicatorText;
        marketDataFeedHealthIndicator.AccessibleDescription = _viewModel.MarketDataFeedStateText;
        marketDataFeedHealthIndicator.ToolTipText = _viewModel.MarketDataFeedStateText;
        (marketDataFeedHealthIndicator.BackColor, marketDataFeedHealthIndicator.ForeColor) =
            MarketDataFeedHealthColors(_viewModel.MarketDataFeedHealthState);
    }

    internal static (Color Background, Color Foreground) MarketDataFeedColors(
        bool isMarketDataFeedActive)
        => isMarketDataFeedActive
            ? (Color.Black, Color.Red)
            : (Color.Black, Color.LimeGreen);

    internal static (Color Background, Color Foreground) MarketDataFeedHealthColors(
        MarketDataFeedHealthState state)
        => state switch
        {
            MarketDataFeedHealthState.Healthy => (Color.LimeGreen, Color.Black),
            MarketDataFeedHealthState.Intermittent => (Color.Yellow, Color.Black),
            MarketDataFeedHealthState.Failed or MarketDataFeedHealthState.Critical
                => (Color.Red, Color.White),
            MarketDataFeedHealthState.OutsidePositionEntryWindow
                => (Color.SteelBlue, Color.White),
            _ => (Color.DimGray, Color.White)
        };

    private void RenderStatusLine()
    {
        var status = string.IsNullOrWhiteSpace(_viewModel.StatusLine)
            ? "Ready"
            : _viewModel.StatusLine;
        lblStatus.Text = status;
        // ToolStripStatusLabel does not consistently expose its WinForms Name as a UIA id.
        // Keeping the accessible name synchronized gives operators and system tests the
        // same current one-line status text.
        lblStatus.AccessibleName = status;
    }

    private void RenderLatestError()
    {
        if (_viewModel.LastError is not { } error || error.Sequence <= _lastErrorSequence)
            return;

        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
    }

    private async void IFMApp_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (_shutdownComplete)
            return;

        e.Cancel = true;
        if (_shutdownStarted)
            return;
        _shutdownStarted = true;
        _viewModel.StartupOperation.Cancel();
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        try
        {
            var presentationShutdown = ShutdownPresentationAsync();
            try
            {
                await presentationShutdown.WaitAsync(PresentationShutdownTimeout);
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine(
                    $"Presentation cleanup exceeded {PresentationShutdownTimeout.TotalSeconds:F0} seconds; " +
                    "continuing with transport shutdown.");
                _ = presentationShutdown.ContinueWith(
                    completed => _ = completed.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            _shutdownComplete = true;
            Close();
        }
        catch (Exception ex)
        {
            _shutdownStarted = false;
            this.ShowErrorMessage(ex.Message, "Application Shutdown Error");
        }

        async Task ShutdownPresentationAsync()
        {
            await ((IAsyncFormControl)economicCalendarView1).CloseAsync();
            await _viewModel.ShutdownOperation.ExecuteAsync();
            statusConsoleView1.UnloadView();
            await _viewModel.DisposeAsync();
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
                [.. _viewModel.BaseContracts],
                _referenceDataService));
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
                            tabPage.UseVisualStyleBackColor = false;
                            tabPage.BackColor = Color.Black;
                            tabPage.Controls.Clear();
                            tabPage.Controls.Add(_tradeBlotter);
                            tabTradeBlotter.Visible = true;
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
            view.LoadViewModel(new MarketDataViewModel(_referenceDataService)));
    }

    private async void marketDataFeedButton_Click(object sender, EventArgs e)
    {
        try
        {
            await _viewModel.ToggleMarketDataFeedAsync();
        }
        catch (Exception ex)
        {
            this.ShowErrorMessage(ex.Message, "Market Data Feed Error");
        }
    }

    private void fundButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<FundTransactionEditor>(view =>
            view.LoadViewModel(new FundTransactionEditorViewModel(_appRoot)));
    }

    private void referenceButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<ReferenceForm>(view =>
            view.LoadViewModel(new ReferenceViewModel(_referenceDataService)));
    }

    private void systemAdminButton_Click(object sender, EventArgs e)
    {
        _navigator.ShowModal<SystemAdminForm>(view =>
            view.LoadViewModel(new SystemAdminViewModel(_referenceDataService)));
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
        {
            btnCloseOrder.Visible = false;
            tabTradeBlotter.Visible = false;
        }
    }

    private void operationViewSplitter_SplitterMoved(object sender, SplitterEventArgs e)
    {
        operationViewSplitter.Invalidate();
        ResizeTabPages();
    }

    private void marketViewSplitter_SplitterMoved(object sender, SplitterEventArgs e)
    {
        marketViewSplitter.Invalidate();
        ResizeTabPages();
    }

    private static void DashboardSplitter_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not SplitContainer splitter)
            return;

        var splitterBounds = splitter.SplitterRectangle;
        using var separatorPen = new Pen(Color.Gray, 1F);
        if (splitter.Orientation == Orientation.Vertical)
        {
            var separatorX = splitterBounds.Left + (splitterBounds.Width / 2);
            e.Graphics.DrawLine(
                separatorPen,
                separatorX,
                splitterBounds.Top,
                separatorX,
                splitterBounds.Bottom - 1);
            return;
        }

        var separatorY = splitterBounds.Top + (splitterBounds.Height / 2);
        e.Graphics.DrawLine(
            separatorPen,
            splitterBounds.Left,
            separatorY,
            splitterBounds.Right - 1,
            separatorY);
    }

    private void InitializeDashboardSplitters()
    {
        var dashboardWidth = operationViewSplitter.ClientSize.Width;
        if (dashboardWidth <= 0)
            return;

        var sidePanelWidth = (int)Math.Round(
            dashboardWidth * DefaultSidePanelWidthRatio,
            MidpointRounding.AwayFromZero);
        operationViewSplitter.SplitterDistance = Math.Clamp(
            sidePanelWidth,
            operationViewSplitter.Panel1MinSize,
            dashboardWidth - operationViewSplitter.SplitterWidth - operationViewSplitter.Panel2MinSize);

        var marketSplitterWidth = marketViewSplitter.ClientSize.Width;
        marketViewSplitter.SplitterDistance = Math.Clamp(
            marketSplitterWidth - marketViewSplitter.SplitterWidth - sidePanelWidth,
            marketViewSplitter.Panel1MinSize,
            marketSplitterWidth - marketViewSplitter.SplitterWidth - marketViewSplitter.Panel2MinSize);
    }


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
            {
                btnCloseOrder.Visible = false;
                tabTradeBlotter.Visible = false;
            }
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
        await economicCalendarView1.LoadViewAsync(_appRoot, _economicCalendarService);
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
