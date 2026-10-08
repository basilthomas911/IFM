using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using MessagePack;
using System.Reflection;
using System.Globalization;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.Views.App;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

public partial class IronCondorTradeView : DarkTradingView, IAsyncFormControl
{
    const int MinimumTradeBlotterWidth = 640;
    const int MinimumChartWidth = 220;
    const int MinimumGraphPaneHeight = 180;
    const int RealtimeDataPaneHeight = 169;
    const int InitialContractIdsPaneHeight = 120;
    const int TradeLimitPaneHeight = 79;
    const int LogPaneHeightDivisor = 3;
    const int VisibleContractIdCount = 4;
    readonly Control _parentControl;
    readonly IronCondorViewModel _viewModel;
    readonly Dictionary<ActionState, Color> _tradePlanStateMap;
    readonly TableLayoutPanel _primaryTopLayout;
    bool _closed;
    readonly Label _initialLoading;
    Task? _initialLoad;
    Task _historyLoad = Task.CompletedTask;
    bool _preparingInitialContent = true;
    bool _renderingLiveFeed;
    bool _updatingSpreadHeaders;
    long _lastErrorSequence;
    int _contractIdsPaneHeight = InitialContractIdsPaneHeight;

    /// <summary>Gets whether this blotter is permanently constrained to historical query-only behavior.</summary>
    public bool IsHistoricalReadOnly => _viewModel.IsHistoricalReadOnly;

    /// <summary>
    /// Initializes a new instance of the <see cref="IronCondorTradeView"/> class with the specified parent control and view
    /// model.
    /// </summary>
    /// <remarks>This constructor sets up the initial state of the view, including configuring UI elements
    /// such as the live feed dropdown and progress bar. The view is associated with a parent control for layout
    /// purposes and uses a view model to populate its data.</remarks>
    /// <param name="parentControl">The parent control that hosts this view. This control is used to manage layout and resizing.</param>
    /// <param name="viewModel">The view model that provides data and commands for the view. It must not be null.</param>
    public IronCondorTradeView(Control parentControl, IronCondorViewModel viewModel)
    {
        InitializeComponent();
        ConfigureSingleGraph();
        _primaryTopLayout = ConfigurePrimaryLayout();
        TradeOrderTypography.Apply(this);
        pnlIronCondorTradeDataRt.SizeChanged += (_, _) => FitSpreadLegHeaders();
        foreach (Control panel in pnlIronCondorTradeDataRt.Controls)
            if (pnlIronCondorTradeDataRt.GetRow(panel) == 0)
                foreach (var label in panel.Controls.OfType<Label>())
                {
                    label.AutoSize = false;
                    label.AutoEllipsis = false;
                    label.Padding = new Padding(2);
                    label.TextChanged += (_, _) => FitSpreadLegHeaders();
                    label.FontChanged += (_, _) => FitSpreadLegHeaders();
                }
        FitSpreadLegHeaders();
        Dock = DockStyle.Fill;
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
        if (_viewModel.IsHistoricalReadOnly)
        {
            ddlLiveFeed.Visible = false;
            ddlLiveFeed.Enabled = false;
            AccessibleName = $"Read-only historical Iron Condor trade {_viewModel.OrderId}:{_viewModel.TradeId}";
        }
        _viewModel.PropertyChanged += ViewModelPropertyChanged;
        _initialLoading = new Label
        {
            Name = "tradeDetailsLoading",
            Text = "Loading trade details...",
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(_initialLoading);
        _initialLoading.BringToFront();
        Disposed += (_, _) =>
        {
            _closed = true;
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= 0x02000000; // Composite native children as the cover is removed.
            return parameters;
        }
    }

    bool CanPresentInitialContent => !_closed && !IsDisposed && Parent is not null;

    /// <summary>Places the single strategy graph beside the position history grid.</summary>
    void ConfigureSingleGraph()
    {
        graphEodData.Parent?.Controls.Remove(graphEodData);
        graphEodData.Visible = false;
        graphSpreadDistribution.Dock = DockStyle.Fill;
        graphSpreadDistribution.Margin = Padding.Empty;
    }

    TableLayoutPanel ConfigurePrimaryLayout()
    {
        var topLayout = new TableLayoutPanel
        {
            Name = "pnlIronCondorTopLayout",
            AccessibleName = "Trade history and graphs",
            BackColor = Color.FromArgb(64, 64, 64),
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        topLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, graphEodData.Width / 2F));
        topLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        pnlIronCondorTrade.Dock = DockStyle.Fill;
        pnlIronCondorTrade.Margin = Padding.Empty;
        graphSpreadDistribution.Dock = DockStyle.Fill;
        graphSpreadDistribution.Margin = Padding.Empty;
        topLayout.Controls.Add(pnlIronCondorTrade, 0, 0);
        topLayout.Controls.Add(graphSpreadDistribution, 1, 0);
        pnlRealTimeData.Panel1.Controls.Add(topLayout);

        pnlRealTimeData.Dock = DockStyle.Fill;
        pnlRealTimeData.Margin = Padding.Empty;
        pnlAssetSplitter.Panel1.Controls.Add(pnlRealTimeData);
        pnlRealTimeData.BringToFront();

        ConfigureRealTimeVerticalSpacing();
        pnlTradeHistory.MinimumSize = new Size(0, _contractIdsPaneHeight);
        pnlTradeHistory.MaximumSize = new Size(0, _contractIdsPaneHeight);
        pnlTradeLimit.Height = TradeLimitPaneHeight;
        lstTradeLimit.Dock = DockStyle.Fill;
        MakeHeaderTableFullWidth(tableLayoutPanel1);
        MakeHeaderTableFullWidth(pnlRt);
        MakeHeaderTableFullWidth(pnlIronCondorTradeDataRt);
        return topLayout;

        void MakeHeaderTableFullWidth(TableLayoutPanel table)
        {
            table.AutoSize = false;
            var weights = table.ColumnStyles.Cast<ColumnStyle>()
                .Select(style => Math.Max(style.Width, 1F))
                .ToArray();
            var firstFlexibleColumn = table == pnlIronCondorTradeDataRt ? 1 : 0;
            if (firstFlexibleColumn == 1)
            {
                var spreadNames = new[] { "PutCreditSpread", "PutDebitSpread", "CallCreditSpread", "CallDebitSpread" };
                table.ColumnStyles[0].SizeType = SizeType.Absolute;
                table.ColumnStyles[0].Width = Math.Max(180,
                    spreadNames.Max(name => TextRenderer.MeasureText(name, txtCallSpreadType.Font).Width) + 16);
            }
            var total = weights.Skip(firstFlexibleColumn).Sum();
            for (var index = firstFlexibleColumn; index < table.ColumnStyles.Count; index++)
            {
                table.ColumnStyles[index].SizeType = SizeType.Percent;
                table.ColumnStyles[index].Width = weights[index] / total * 100F;
            }

            table.Width = Math.Max(1, pnlRealTimeHeaderData.ClientSize.Width - table.Left - 4);
            table.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        }
    }

    void ConfigureRealTimeVerticalSpacing()
    {
        tableLayoutPanel1.AutoSize = false;
        tableLayoutPanel1.Height = 36;
        tableLayoutPanel1.RowStyles[0].SizeType = SizeType.Absolute;
        tableLayoutPanel1.RowStyles[0].Height = 34F;

        pnlRt.Top = tableLayoutPanel1.Bottom;
        pnlRt.Height = 32;

        pnlIronCondorTradeDataRt.Top = pnlRt.Bottom + 3;
        pnlIronCondorTradeDataRt.RowStyles[0].SizeType = SizeType.Absolute;
        pnlIronCondorTradeDataRt.RowStyles[0].Height = 36F;
        pnlIronCondorTradeDataRt.RowStyles[1].SizeType = SizeType.Absolute;
        pnlIronCondorTradeDataRt.RowStyles[1].Height = 25F;
        pnlIronCondorTradeDataRt.RowStyles[2].SizeType = SizeType.Absolute;
        pnlIronCondorTradeDataRt.RowStyles[2].Height = 25F;
        pnlIronCondorTradeDataRt.Height = 86;
        pnlRealTimeHeaderData.Height = pnlIronCondorTradeDataRt.Bottom + 4;
    }

    /// <summary>Reserves enough height for every wrapped spread-leg heading and both value rows.</summary>
    void FitSpreadLegHeaders()
    {
        if (_updatingSpreadHeaders || IsDisposed) return;
        _updatingSpreadHeaders = true;
        try
        {
            var widths = pnlIronCondorTradeDataRt.GetColumnWidths();
            var headerHeight = 52;
            foreach (Control panel in pnlIronCondorTradeDataRt.Controls)
            {
                if (pnlIronCondorTradeDataRt.GetRow(panel) != 0) continue;
                var column = pnlIronCondorTradeDataRt.GetColumn(panel);
                if (column < 0 || column >= widths.Length) continue;
                foreach (var label in panel.Controls.OfType<Label>())
                {
                    var textWidth = Math.Max(1, widths[column] - panel.Margin.Horizontal - label.Padding.Horizontal);
                    var measured = TextRenderer.MeasureText(label.Text, label.Font,
                        new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                    headerHeight = Math.Max(headerHeight, measured.Height + panel.Margin.Vertical + label.Padding.Vertical + 6);
                }
            }
            pnlIronCondorTradeDataRt.RowStyles[0].Height = headerHeight;
            pnlIronCondorTradeDataRt.Height = headerHeight + 50;
            pnlRealTimeHeaderData.Height = pnlIronCondorTradeDataRt.Bottom + 4;
            SetSplitterDistance(pnlRealTimeData, pnlRealTimeData.ClientSize.Height
                - pnlRealTimeData.SplitterWidth - Math.Max(RealtimeDataPaneHeight, pnlRealTimeHeaderData.Height));
        }
        finally { _updatingSpreadHeaders = false; }
    }

    /// <summary>
    /// Adjusts the size and layout of the control based on the specified parent control.
    /// </summary>
    /// <remarks>This method resizes the control to match the size of the <paramref name="parentControl"/> and
    /// adjusts the layout of internal panels and graphs accordingly.</remarks>
    /// <param name="parentControl">The parent control whose size is used to adjust the layout of this control.</param>
    void IFormControl.Resize(Control parentControl)
    {
        ArgumentNullException.ThrowIfNull(parentControl);
        Dock = DockStyle.Fill;
        var parentSize = parentControl.ClientSize;
        if (parentSize.Width <= 0 || parentSize.Height <= 0)
        {
            graphSpreadDistribution.Visible = false;
            return;
        }

        SuspendLayout();
        try
        {
            Size = parentSize;
            var logPaneHeight = Math.Max(pnlAssetSplitter.Panel2MinSize,
                (pnlAssetSplitter.ClientSize.Height - pnlAssetSplitter.SplitterWidth)
                / LogPaneHeightDivisor);
            SetSplitterDistance(pnlAssetSplitter,
                pnlAssetSplitter.ClientSize.Height
                - pnlAssetSplitter.SplitterWidth
                - logPaneHeight);
            var formerGraphWidth = Math.Max(0, Width - MinimumTradeBlotterWidth - 10);
            var graphWidth = formerGraphWidth / 2;
            var maximumTopHeight = pnlRealTimeData.ClientSize.Height
                - pnlRealTimeData.SplitterWidth
                - Math.Max(RealtimeDataPaneHeight, pnlRealTimeHeaderData.Height);
            var graphPaneHeight = Math.Max(pnlRealTimeData.Panel1MinSize, maximumTopHeight);
            var chartsAreSafe = graphWidth >= MinimumChartWidth
                && graphPaneHeight >= MinimumGraphPaneHeight;
            _primaryTopLayout.ColumnStyles[1].Width = chartsAreSafe ? graphWidth : 0F;
            graphSpreadDistribution.Visible = chartsAreSafe;
            pnlRealTimeData.Visible = true;
            SetSplitterDistance(pnlRealTimeData, graphPaneHeight);
            _primaryTopLayout.PerformLayout();
            SetSplitterDistance(pnlTradeSplitter,
                pnlTradeSplitter.ClientSize.Height
                - pnlTradeSplitter.SplitterWidth
                - _contractIdsPaneHeight
                - TradeLimitPaneHeight);
        }
        finally
        {
            ResumeLayout(performLayout: true);
        }
    }

    static void SetSplitterDistance(SplitContainer splitter, int preferredDistance)
    {
        var length = splitter.Orientation == Orientation.Vertical
            ? splitter.ClientSize.Width
            : splitter.ClientSize.Height;
        var maximum = length - splitter.SplitterWidth - splitter.Panel2MinSize;
        if (maximum < splitter.Panel1MinSize)
            return;
        splitter.SplitterDistance = Math.Clamp(preferredDistance, splitter.Panel1MinSize, maximum);
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
        UiExceptionReporter.Observe(((IAsyncFormControl)this).CloseAsync(), nameof(IAsyncFormControl.CloseAsync), this);
    }

    async ValueTask IAsyncFormControl.CloseAsync()
    {
        if (_closed)
            return;
        _closed = true;
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        await _viewModel.DisposeAsync();
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
    async void IronCondorControl_Load(object sender, EventArgs e)
        => await (_initialLoad ??= LoadInitialContentAsync());

    async Task LoadInitialContentAsync()
    {
        lstTradePlanAction.SetDoubleBuffered(true);
        lstTradeHistory.SetDoubleBuffered(true);
        lstTradeHistory.Enabled = true;
        lblTradeDescription.Text = _viewModel.IsHistoricalReadOnly
            ? $"READ-ONLY HISTORICAL TRADE | {_viewModel.Fund.Name} | {_viewModel.FundOrder.OperatorReference}"
            : $"{_viewModel.Fund.Name} | {_viewModel.FundOrder.OperatorReference}";
        try
        {
            if (_viewModel.EstablishedTrade is not null)
            {
                var established = await _viewModel.LoadEstablishedTradeAsync();
                if (!CanPresentInitialContent) return;
                ShowEstablishedTrade(established);
                ((IFormControl)this).Resize(Parent!);
                _preparingInitialContent = false;
                _initialLoading.Visible = false;
                return;
            }
            if (!_viewModel.IsHistoricalReadOnly)
                await _viewModel.EnableMarketDataFeedResetListener();
            if (!CanPresentInitialContent) return;
            var trade = await _viewModel.LoadIronCondorTrade();
            if (!CanPresentInitialContent) return;
            if (trade is null)
            {
                if (_viewModel.LastError is null)
                {
                    this.ShowErrorMessage(
                        $"No Iron Condor Trade found for orderId: {_viewModel.OrderId} tradeId: {_viewModel.TradeId}",
                        "Loading Trade");
                }
                _initialLoading.Text = "No trade details are available.";
                return;
            }

            await _viewModel.LoadIronCondorTradeDetailsAsync(
                trade,
                _viewModel.OrderId,
                _viewModel.TradeId);
            if (!CanPresentInitialContent) return;
            // Apply queued snapshots and finish the initially selected history row before reveal.
            await InvokeAsync((Action)(() =>
            {
                if (CanPresentInitialContent) RenderTradeHistory();
            }));
            if (!CanPresentInitialContent) return;
            await _historyLoad;
            if (!CanPresentInitialContent) return;
            await InvokeAsync((Action)(() =>
            {
                if (!CanPresentInitialContent) return;
                ((IFormControl)this).Resize(Parent!);
                PerformLayout();
                _preparingInitialContent = false;
                _initialLoading.Visible = false;
                Invalidate(true);
            }));
        }
        catch (Exception exception)
        {
            if (!CanPresentInitialContent) return;
            _initialLoading.Text = $"Unable to load trade details: {exception.Message}";
            this.ShowErrorMessage(exception.Message, "Loading Iron Condor Monitor Error");
        }
    }

    /// <summary>Populates the original strategy view from persisted financial trade legs and fills.</summary>
    /// <param name="trade">The exact execution-created Iron Condor trade.</param>
    void ShowEstablishedTrade(EstablishedTradeDefinition trade)
    {
        ClearUnloadedValues(this);
        Name = "IronCondorTradeView";
        AccessibleName = $"Iron Condor trade {trade.Id.Format()} {trade.Status}";
        lblTradeDescription.Text = $"{_viewModel.Fund.Name} | {_viewModel.FundOrderTrade.TradeType} | {trade.Id.Format()} | {trade.Status}";
        lstTradeInfo.Items.Clear();
        lstTradeInfo.Items.Add(new ListViewItem(new[] {
            trade.Id.OrderId.ToString(), trade.Id.TradeId.ToString(), _viewModel.FundOrderTrade.TradeType.ToString(),
            trade.Legs.Min(leg => Math.Abs(leg.SignedQuantity)).ToString(),
            trade.EstablishedAtUtc.ToString("yyyy-MM-dd"), trade.Legs.Select(leg => leg.Expiry).FirstOrDefault()?.ToString("yyyy-MM-dd") ?? "",
            trade.Status.ToString(), _viewModel.FundOrderTrade.TradeAction.ToString() }));
        lstOptionContractIds.Items.Clear();
        foreach (var leg in trade.Legs) lstOptionContractIds.Items.Add(leg.ContractId);
        FitContractIdPaneToFourRows();
        ShowTradeHistory(_viewModel.TradeHistory);
        if (lstTradeHistory.Items.Count > 0) lstTradeHistory.Items[0].Selected = true;
        txtCallLongStrike.Text = Strike(1, true); txtCallShortStrike.Text = Strike(1, false);
        txtPutLongStrike.Text = Strike(2, true); txtPutShortStrike.Text = Strike(2, false);
        txtPutSpreadType.Text = _viewModel.PutSpreadTradeType.ToString();
        txtCallSpreadType.Text = _viewModel.CallSpreadTradeType.ToString();
        txtRtTradeStatus.Text = trade.Status.ToString();
        txtRtNetSpread.Text = trade.OpeningValue.ToString("0.00");
        txtRtValueDate.Text = trade.EstablishedAtUtc.ToString("yyyy-MM-dd");
        ddlLiveFeed.Enabled = true;
        foreach (var series in graphSpreadDistribution.Series) series.Points.Clear();
        var spread = graphSpreadDistribution.Series[3];
        spread.MarkerStyle = System.Windows.Forms.DataVisualization.Charting.MarkerStyle.Circle;
        spread.MarkerSize = 8;
        spread.Points.AddXY(EasternTime.FromUtc(trade.EstablishedAtUtc), trade.OpeningValue);
        graphSpreadDistribution.ChartAreas[0].RecalculateAxesScale();

        string Strike(byte right, bool bought) => trade.Legs.Single(leg => leg.PutCall == right
            && (leg.SignedQuantity > 0) == bought).Strike?.ToString("0.##") ?? "";
    }

    /// <summary>Clears designer sample values until their actual data source is available.</summary>
    /// <param name="parent">The strategy control subtree.</param>
    static void ClearUnloadedValues(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is TextBoxBase textBox) textBox.Text = string.Empty;
            ClearUnloadedValues(control);
        }
    }

    void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (_closed || eventArgs.PropertyName == nameof(IronCondorViewModel.UiDispatchMetrics))
            return;

        var postedAt = Stopwatch.GetTimestamp();
        this.Post(() =>
        {
            if (_closed)
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

    void RenderProperty(string? propertyName)
    {
        switch (propertyName)
        {
            case nameof(IronCondorViewModel.LastError):
                ShowLatestError();
                break;
            case nameof(IronCondorViewModel.FuturesEodHistory):
                RenderFuturesEodHistory();
                break;
            case nameof(IronCondorViewModel.FuturesEodRevision):
                if (_viewModel.CurrentFuturesEodData is not null)
                    ShowFuturesEodData(_viewModel.CurrentFuturesEodData);
                break;
            case nameof(IronCondorViewModel.TradeInfo):
                ShowTradeInfo(_viewModel.TradeInfo);
                break;
            case nameof(IronCondorViewModel.TradeLimitSnapshot):
                if (_viewModel.TradeLimitSnapshot is { } limits)
                    ShowTradeLimits(limits.OrderId, (limits.TradeLimit, limits.FundBalance));
                break;
            case nameof(IronCondorViewModel.IronCondorLegObservations):
                RenderEstablishedLegs();
                break;
            case nameof(IronCondorViewModel.IronCondorPosition):
                RenderEstablishedPosition();
                break;
            case nameof(IronCondorViewModel.IronCondorPlanHistory):
                foreach (var historicalPlan in _viewModel.IronCondorPlanHistory.Reverse())
                    RenderEstablishedTradePlan(historicalPlan, false);
                if (_viewModel.StrategyTradePlan is { } latestPlan) RenderEstablishedTradePlan(latestPlan);
                break;
            case nameof(IronCondorViewModel.StrategyTradePlan):
                if (_viewModel.StrategyTradePlan is { } plan)
                    RenderEstablishedTradePlan(plan);
                break;
            case nameof(IronCondorViewModel.PositionRevision):
                if (_viewModel.PositionSnapshot is { } position)
                    ShowIronCondorTradePosition(
                        position.Key,
                        (position.PutSpread, position.CallSpread),
                        position.TradeLimit,
                        position.OpeningNetSpread,
                        position.FundBalance);
                break;
            case nameof(IronCondorViewModel.SpreadBarData):
                ShowOptionTradeSpreadBarData(_viewModel.SpreadBarData);
                break;
            case nameof(IronCondorViewModel.TradeHistory):
                if (!_preparingInitialContent && !_viewModel.IsLoading)
                    RenderTradeHistory();
                break;
            case nameof(IronCondorViewModel.TradePlans):
                ShowTradePlans(_viewModel.TradePlans);
                break;
            case nameof(IronCondorViewModel.IsLoaded):
                if (!_preparingInitialContent && _viewModel.IsLoaded)
                    RenderTradeHistory();
                break;
            case nameof(IronCondorViewModel.IsLiveFeedEnabled):
                RenderLiveFeedState();
                break;
            case nameof(IronCondorViewModel.IsLoading):
                ddlLiveFeed.Enabled = !_viewModel.IsHistoricalReadOnly && !_viewModel.IsLoading && (_viewModel.EstablishedTrade is not null || _viewModel.FuturesEodHistory.Length > 0);
                break;
        }
    }

    /// <summary>Renders only actual backend option observations in the selected four leg controls.</summary>
    void RenderEstablishedLegs()
    {
        if (_viewModel.EstablishedTrade is not { } trade) return;
        foreach (var leg in trade.Legs)
        {
            if (!_viewModel.IronCondorLegObservations.TryGetValue(leg.ContractId, out var quote)) continue;
            var prefix = (leg.PutCall == 1 ? "txtCall" : "txtPut") + (leg.SignedQuantity > 0 ? "Long" : "Short");
            Set("Bid", quote.BidPrice, "0.00"); Set("Ask", quote.AskPrice, "0.00");
            var hasGreeks = quote.GreeksAvailable != false;
            Set("Delta", hasGreeks ? quote.Delta : double.NaN, "0.####"); Set("Gamma", hasGreeks ? quote.Gamma : double.NaN, "0.#####");
            Set("Theta", hasGreeks ? quote.Theta : double.NaN, "0.####"); Set("ImpliedVol", hasGreeks ? quote.ImpliedVolatility : double.NaN, "P2");
            void Set(string suffix, double value, string format)
            {
                var control = Controls.Find(prefix + suffix, true).FirstOrDefault();
                if (control is not null) control.Text = double.IsFinite(value) ? value.ToString(format) : "N/A";
            }
        }
    }

    /// <summary>Plots the actual per-unit strategy spread from a coherent backend position.</summary>
    void RenderEstablishedPosition()
    {
        if (_viewModel.IronCondorPosition is not { } position || _viewModel.EstablishedTrade is null)
            return;
        var quantity = position.Legs.Min(leg => Math.Abs(leg.SignedQuantity));
        if (quantity < 1) return;
        var spreadPrice = position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) / quantity;
        txtRtNetSpread.Text = spreadPrice.ToString("0.00");
        var series = graphSpreadDistribution.Series[3];
        var observed = EasternTime.FromUtc(position.AsOfUtc).ToOADate();
        series.Points.AddXY(observed, spreadPrice);
        graphSpreadDistribution.AccessibleName = $"Iron Condor observed spread price {spreadPrice:0.00}; position {position.PositionSequence}";
        graphSpreadDistribution.AccessibleDescription = $"Centered on {EasternTime.FromUtc(position.AsOfUtc):yyyy-MM-dd HH:mm:ss}";
        while (series.Points.Count > 500) series.Points.RemoveAt(0);
        var area = graphSpreadDistribution.ChartAreas[0];
        var halfWindow = TimeSpan.FromMinutes(5).TotalDays;
        area.AxisX.Minimum = observed - halfWindow;
        area.AxisX.Maximum = observed + halfWindow;
        area.RecalculateAxesScale();
        graphSpreadDistribution.Invalidate();
    }

    void RenderFuturesEodHistory()
    {
        var history = _viewModel.FuturesEodHistory;
        ShowFuturesEodDataHistory(history);
        foreach (var series in graphEodData.Series)
            series.Points.Clear();
        foreach (var value in history)
        {
            graphEodData.Series[0].Points.AddXY(value.ValueDate.ToDateTime(TimeOnly.MinValue), value.ClosePrice);
            graphEodData.Series[1].Points.AddXY(value.ValueDate.ToDateTime(TimeOnly.MinValue), value.UpperBand);
            graphEodData.Series[2].Points.AddXY(value.ValueDate.ToDateTime(TimeOnly.MinValue), value.Mean);
            graphEodData.Series[3].Points.AddXY(value.ValueDate.ToDateTime(TimeOnly.MinValue), value.LowerBand);
        }
        graphEodData.ChartAreas[0].RecalculateAxesScale();
        graphEodData.Update();
        ddlLiveFeed.Enabled = !_viewModel.IsHistoricalReadOnly && !_viewModel.IsLoading;
    }

    void RenderTradeHistory()
    {
        var history = _viewModel.TradeHistory;
        ShowTradeHistory(history);
        var index = history.Length - 1;
        if (index < 0 || index >= lstTradeHistory.Items.Count)
            return;
        if (!lstTradeHistory.Items[index].Selected)
            lstTradeHistory.Items[index].Selected = true;
        lstTradePlanAction.Focus();
    }

    void RenderLiveFeedState()
    {
        _renderingLiveFeed = true;
        ddlLiveFeed.SelectedItem = _viewModel.IsLiveFeedEnabled
            ? IronCondorViewModel.LiveFeedOn
            : IronCondorViewModel.LiveFeedOff;
        ddlLiveFeed.Font = new Font(
            ddlLiveFeed.Font,
            _viewModel.IsLiveFeedEnabled ? FontStyle.Bold : FontStyle.Regular);
        _renderingLiveFeed = false;
    }

    void ShowLatestError()
    {
        var error = _viewModel.LastError;
        if (error is null || error.Sequence <= _lastErrorSequence)
            return;
        _lastErrorSequence = error.Sequence;
        this.ShowErrorMessage(error.Message, error.Caption);
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
                $"{(_viewModel.EstablishedTrade is null ? Math.Abs(e.NetSpread) : e.NetSpread):F2}",
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
        FitContractIdPaneToFourRows();
    }

    void FitContractIdPaneToFourRows()
    {
        if (lstOptionContractIds.Items.Count == 0)
            return;

        lstOptionContractIds.CreateControl();
        var firstRow = lstOptionContractIds.GetItemRect(0);
        if (firstRow.Height <= 0)
            return;

        _contractIdsPaneHeight = firstRow.Top
            + firstRow.Height * VisibleContractIdCount
            + 3;
        pnlTradeHistory.MinimumSize = new Size(0, _contractIdsPaneHeight);
        pnlTradeHistory.MaximumSize = new Size(0, _contractIdsPaneHeight);
        SetSplitterDistance(pnlTradeSplitter,
            pnlTradeSplitter.ClientSize.Height
            - pnlTradeSplitter.SplitterWidth
            - _contractIdsPaneHeight
            - TradeLimitPaneHeight);
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
            SetBGColor(txtCallLongImpliedVol, longCallOptionLeg.ImpliedVolatility, $"{ToDoublePercent(txtCallLongImpliedVol.Text.Replace("%", ""))}", "P");
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
        txtRtTradePnl.Text = $"{dailyPnl:C}";
        txtRtTradePnl.BackColor = dailyPnl >= 0.0m ? Color.LimeGreen : Color.Red;
        txtRtNetSpread.Text = $"{Math.Abs(netSpread):C}";
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
            tradePnlValue = tradePnlValue > maxLoss ? tradePnlValue : maxLoss;
            DisplayPercentLoss(maxLoss);
        }
        ddlLiveFeed.Enabled = !_viewModel.IsHistoricalReadOnly && _viewModel.ValueDate.HasValue;
        return;

        double ToDoublePercent(string percentText)
            => string.IsNullOrWhiteSpace(percentText)
                ? 0.0
                : Convert.ToDouble(percentText.Replace("%", "")) / 100;

        void DisplayPercentProfit(int maxProfit)
        {
            pbPercentProfit.SetState(1);
            pbPercentProfit.Visible = true;
            pbPercentProfit.Maximum = (int)fundBalance;
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
            txtTradePnl.Text = $"{tradePnl:C} @ {percent:P2} loss";
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

    static readonly PropertyInfo[] monitoringColumns = typeof(IronCondorTradePlanSnapshot).GetProperties()
        .Where(property => property.GetCustomAttribute<KeyAttribute>() is { IntKey: >= 0 and <= 47 })
        .OrderBy(property => property.GetCustomAttribute<KeyAttribute>()!.IntKey).ToArray();
    bool monitoringColumnsInitialized;

    /// <summary>Displays nullable legacy business values directly and bounds live plan history without inventing defaults.</summary>
    /// <param name="plan">The newest coherent backend plan accepted by the view model.</param>
    void RenderEstablishedTradePlan(StrategyTradePlanSnapshot plan, bool updateCurrentValues = true)
    {
        if (plan.IronCondorTradePlanSnapshot is not { } snapshot) return;
        if (updateCurrentValues)
        {
        txtRtTradeStatus.Text = $"{plan.State}: {plan.Explanation}";
        txtRtMscore.Text = snapshot.MScore?.ToString("F4", CultureInfo.InvariantCulture) ?? "N/A";
        txtRtMscore.BackColor = snapshot.IsComplete && Enum.TryParse<ActionState>(snapshot.ActionState, out var severity)
            ? _tradePlanStateMap.GetValueOrDefault(severity, Color.Black) : Color.Black;
        txtRtAssetPrice.Text = snapshot.AssetPrice?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A";
        // The central spread value and graph use observed leg marks; theoretical prices stay in the plan.
        var quantity = plan.Position.Legs.Length == 4 ? plan.Position.Legs.Min(leg => Math.Abs(leg.SignedQuantity)) : 0;
        if (_viewModel.IronCondorPosition is null && quantity > 0)
            txtRtNetSpread.Text = (plan.Position.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) / quantity)
                .ToString("0.00", CultureInfo.CurrentCulture);
        txtRtTradePnl.Text = snapshot.TradePnl?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A";
        pnlTradePlanAction.BackColor = txtRtMscore.BackColor;
        SetPanelCaption(pnlTradePlanAction, snapshot.ActionType ?? "Monitoring inputs unavailable");
        SetPanelCaption(pnlTradePlanActionReason, snapshot.IsComplete ? snapshot.ActionReason ?? string.Empty
            : string.Join("; ", snapshot.UnavailableReasons));
        }
        var identity = $"{plan.Position.Id.Format()}/{plan.ValueDate}/{plan.Position.RouteGeneration}/{plan.PlanRevision}";
        if (lstTradePlanAction.Items.Cast<ListViewItem>().Any(row => row.Name == identity)) return;
        lstTradePlanAction.BeginUpdate();
        try
        {
            if (!monitoringColumnsInitialized)
            {
                lstTradePlanAction.Items.Clear();
                lstTradePlanAction.Columns.Clear();
                foreach (var column in monitoringColumns)
                    lstTradePlanAction.Columns.Add(column.Name, column.Name.Contains("Reason", StringComparison.Ordinal) ? 320 : 120);
                lstTradePlanAction.ShowItemToolTips = true;
                monitoringColumnsInitialized = true;
            }
            var row = new ListViewItem(monitoringColumns.Select(column => FormatMonitoringValue(column.GetValue(snapshot))).ToArray())
            {
                Name = identity, Tag = snapshot, ToolTipText = string.Join("; ", snapshot.UnavailableReasons),
                ForeColor = snapshot.IsComplete ? Color.White : Color.Yellow, BackColor = Color.Black
            };
            lstTradePlanAction.Items.Insert(0, row);
            while (lstTradePlanAction.Items.Count > 200) lstTradePlanAction.Items.RemoveAt(lstTradePlanAction.Items.Count - 1);
        }
        finally { lstTradePlanAction.EndUpdate(); }
    }

    /// <summary>Formats observed values; missing and nonfinite values are explicitly unavailable.</summary>
    /// <param name="value">The nullable business observation.</param>
    /// <returns>A culture-independent cell value.</returns>
    static string FormatMonitoringValue(object? value) => value switch
    {
        null => "N/A",
        DateTime date => EasternTime.FromUtc(date).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        double number when !double.IsFinite(number) => "N/A",
        IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "N/A"
    };

    /// <summary>Uses a persistent read-only caption so action text survives repaints.</summary>
    /// <param name="panel">The existing action or reason panel.</param>
    /// <param name="text">The backend monitoring explanation.</param>
    static void SetPanelCaption(Panel panel, string text)
    {
        var caption = panel.Controls.OfType<Label>().FirstOrDefault();
        if (caption is null)
        {
            caption = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0), ForeColor = Color.White, BackColor = Color.Transparent };
            panel.Controls.Add(caption);
        }
        caption.Text = text;
    }

    /// <summary>
    /// Displays a single trade plan in the trade plan action list and updates related UI elements.
    /// </summary>
    /// <param name="e">The trade plan view model to display.</param>
    void ShowTradePlan(TradePlanReadModel e)
    {
        var bgColor = e.TradeType == TradeType.ShortIronCondor
            ? e.MScore switch
            {
                >= 0.9 => Color.Red,
                >= 0.8 => Color.Yellow,
                _ => Color.LimeGreen
            }
            : e.MScore switch
            {
                >= 0.9 => Color.LimeGreen,
                >= 0.8 => Color.Yellow,
                _ => Color.Red
            };
        txtRtMscore.BackColor = bgColor;
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
                $"{EasternTime.FromUtc(e.ActionDate):T}",
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
        lstTradePlanAction.BeginUpdate();
        lstTradePlanAction.Items.Clear();
        foreach (var e in tradePlans)
        {
            var tradePlanActionItem = new ListViewItem([
                $"{EasternTime.FromUtc(e.ActionDate):T}",
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
    async void ddlLiveFeed_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_viewModel.IsHistoricalReadOnly || !ddlLiveFeed.Enabled || _renderingLiveFeed)
            return;
        try
        {
            switch ($"{ddlLiveFeed.SelectedItem}")
            {
                case IronCondorViewModel.LiveFeedOn:
                    await _viewModel.EnableLiveFeedAsync();
                    break;
                case IronCondorViewModel.LiveFeedOff:
                    await _viewModel.DisableLiveFeedAsync();
                    break;
            }
        }
        catch (Exception exception)
        {
            this.ShowErrorMessage(exception.Message, "Iron Condor Live Feed Error");
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
    async void lstTradeHistory_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_closed || lstTradeHistory.SelectedIndices.Count != 1 || !lstTradeHistory.Enabled) return;
        var index = lstTradeHistory.SelectedIndices[0];
        if (_viewModel.EstablishedTrade is not null)
        {
            var position = _viewModel.TradeHistory[index];
            txtRtTradeStatus.Text = position.TradeStatus.ToString();
            txtRtNetSpread.Text = position.NetSpread.ToString("0.00");
            txtRtValueDate.Text = position.ValueDate.ToString("yyyy-MM-dd");
            txtRtDaysToExpiry.Text = position.DaysToExpiry.ToString();
            txtRtTradePnl.Text = position.TradePnl.ToString("0.00");
            return;
        }
        await (_historyLoad = LoadSelectedHistoryAsync(index));
    }

    async Task LoadSelectedHistoryAsync(int index)
    {
        try
        {
            await _viewModel.LoadIronCondorTradePosition(index);
            if (_closed) return;
            await _viewModel.LoadOptionTradeSpreadBarData(index);
            if (_closed) return;
            await _viewModel.LoadTradePlans(index);
            if (_closed) return;
            await _viewModel.LoadFuturesEodData(index);
        }
        catch (Exception exception)
        {
            if (_closed) return;
            this.ShowErrorMessage(exception.Message, "Loading Iron Condor History Error");
        }
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
    /// Handles the event when the IronCondorTradeView control is removed. (Currently not implemented)
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The control event arguments.</param>
    void IronCondorTradeView_ControlRemoved(object sender, ControlEventArgs e)
    {

    }

    /// <summary>
    /// Handles the click event for the VX volatility real-time label. (Currently not implemented)
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    void lblVixVolRT_Click(object sender, EventArgs e)
    {

    }
}
