using System.ComponentModel;
using System.Globalization;
using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>WinForms adapter for the framework-neutral Operations presentation state.</summary>
public partial class OperationsView : DarkTradingView
{
    const double StrategyDetailHeightRatio = 0.33;
    const string PriceSeriesName = "ITI Price";
    const string OtherEventSeriesName = "Other ITI Event";
    const string UpEventSeriesName = "Direction Up";
    const string DownEventSeriesName = "Direction Down";
    const string SelectionSeriesName = "Selection";
    const int MinimumTimeColumnWidth = 185;
    OperationsViewModel? _viewModel;
    IReadOnlyList<FuturesItiSignalEventRow>? _renderedEvents;
    IReadOnlyList<StrategyWorkflowRow>? _renderedWorkflows;
    bool _synchronizingSelection;
    bool _synchronizingTimeFrame;
    bool _synchronizingWorkflowSelection;

    public OperationsView()
    {
        InitializeComponent();
        DashboardTypography.ApplyFamilyAndSize(this);
        lblTimeFrame.Dock = DockStyle.None;
        lblTimeFrame.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblTimeFrame.Height = ddlTimeFrame.Height;
        ConfigureChart();
        lstStrategyWorkflows.SetDoubleBuffered(true);
        lstStrategyWorkflows.OwnerDraw = true;
        lstStrategyWorkflows.DrawColumnHeader += (_, e) => e.DrawDefault = true;
        lstStrategyWorkflows.DrawItem += (_, e) => e.DrawDefault = false;
        lstStrategyWorkflows.DrawSubItem += DrawWorkflowSubItem;
        ddlTimeFrame.Items.AddRange(
            [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly]);
        ddlTimeFrame.SelectedItem = TimeFrameType.Daily;
        operationsTabs.SelectedIndex = (int)OperationsViewType.Strategy;
        ResizeStrategyPanels();
        ResizeStrategyContentPanels();
    }

    public void LoadViewModel(OperationsViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        RefreshView(viewModel);
    }

    public void RefreshView(OperationsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        _synchronizingSelection = true;
        try
        {
            if (operationsTabs.SelectedIndex != (int)viewModel.SelectedView)
                operationsTabs.SelectedIndex = (int)viewModel.SelectedView;
        }
        finally
        {
            _synchronizingSelection = false;
        }

        var strategy = viewModel.Strategy;
        _synchronizingTimeFrame = true;
        try
        {
            if (!Equals(ddlTimeFrame.SelectedItem, strategy.SelectedTimeFrame))
                ddlTimeFrame.SelectedItem = strategy.SelectedTimeFrame;
        }
        finally
        {
            _synchronizingTimeFrame = false;
        }

        var strategyHeader = $"Futures ITI - {strategy.ContractId}";
        lblItiStatus.Text = strategy.LastError is null
            ? $"{strategyHeader} | {strategy.StatusText}"
            : $"{strategyHeader} | {strategy.StatusText} | {strategy.LastError.Caption}";
        lblItiStatus.ForeColor = strategy.LastError is not null
            ? Color.Gold
            : strategy.IsListening ? Color.LimeGreen : Color.Silver;

        if (!ReferenceEquals(_renderedEvents, strategy.Events))
        {
            RenderEvents(strategy.Events);
            _renderedEvents = strategy.Events;
        }
        if (!ReferenceEquals(_renderedWorkflows, strategy.Workflows))
        {
            RenderWorkflows(strategy.Workflows);
            _renderedWorkflows = strategy.Workflows;
        }
        if (!string.Equals(txtWorkflowDetails.Text, strategy.SelectedWorkflowDetails, StringComparison.Ordinal))
            txtWorkflowDetails.Text = strategy.SelectedWorkflowDetails;
    }

    void RenderEvents(IReadOnlyList<FuturesItiSignalEventRow> events)
    {
        RenderChart(events);
        if (lstStrategyWorkflows.SelectedItems.Count > 0
            && lstStrategyWorkflows.SelectedItems[0].Tag is StrategyWorkflowRow selected)
            HighlightChartPoint(selected.TriggerStableIdentity);
    }

    void RenderWorkflows(IReadOnlyList<StrategyWorkflowRow> workflows)
    {
        var selectedId = lstStrategyWorkflows.SelectedItems.Count == 0
            ? _viewModel?.Strategy.SelectedWorkflowId
            : (lstStrategyWorkflows.SelectedItems[0].Tag as StrategyWorkflowRow)?.WorkflowId;

        _synchronizingWorkflowSelection = true;
        lstStrategyWorkflows.BeginUpdate();
        try
        {
            lstStrategyWorkflows.Items.Clear();
            foreach (var row in workflows)
            {
                var item = new ListViewItem(FormatListTime(row.OccurredOn, row.TimePeriod))
                {
                    Tag = row,
                    Name = row.WorkflowId.ToString(),
                    ToolTipText = row.PipelineActors.Count == 0
                        ? "No pipeline actor has started."
                        : string.Join(Environment.NewLine, row.PipelineActors.Select(actor => actor.AccessibleStatus))
                };
                item.SubItems.Add(row.SignalEvent.ToStringFast());
                item.SubItems.Add(row.Trend.ToStringFast());
                item.SubItems.Add(row.FuturesPrice.ToString("N2", CultureInfo.InvariantCulture));
                item.SubItems.Add(string.Join(' ', row.PipelineActors.Select(actor =>
                    $"{actor.ShortLabel}:{actor.DisplayState}")));
                item.SubItems.Add(row.EndState);
                lstStrategyWorkflows.Items.Add(item);
                if (row.WorkflowId == selectedId)
                    item.Selected = true;
            }
        }
        finally
        {
            lstStrategyWorkflows.EndUpdate();
            _synchronizingWorkflowSelection = false;
        }

        ResizeTimeColumnToFit();
        if (lstStrategyWorkflows.SelectedItems.Count == 0 && lstStrategyWorkflows.Items.Count > 0)
            lstStrategyWorkflows.Items[0].Selected = true;
        RenderSelectedWorkflow();
    }

    void operationsTabs_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_synchronizingSelection
            || _viewModel is null
            || operationsTabs.SelectedIndex < 0)
        {
            return;
        }

        _viewModel.SelectView((OperationsViewType)operationsTabs.SelectedIndex);
    }

    void lstStrategyWorkflows_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (!_synchronizingWorkflowSelection)
            RenderSelectedWorkflow();
    }

    void ddlTimeFrame_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_synchronizingTimeFrame
            || _viewModel is null
            || ddlTimeFrame.SelectedItem is not TimeFrameType timeFrame)
        {
            return;
        }

        _viewModel.Strategy.SelectedTimeFrame = timeFrame;
        RefreshView(_viewModel);
    }

    void strategySplitter_Resize(object? sender, EventArgs e)
        => ResizeStrategyPanels();

    void strategyContentSplitter_Resize(object? sender, EventArgs e)
        => ResizeStrategyContentPanels();

    void ResizeStrategyPanels()
    {
        var availableHeight = strategySplitter.ClientSize.Height - strategySplitter.SplitterWidth;
        if (availableHeight < strategySplitter.Panel1MinSize + strategySplitter.Panel2MinSize)
            return;

        var listHeight = (int)Math.Round(
            availableHeight * (1 - StrategyDetailHeightRatio),
            MidpointRounding.AwayFromZero);
        strategySplitter.SplitterDistance = Math.Clamp(
            listHeight,
            strategySplitter.Panel1MinSize,
            availableHeight - strategySplitter.Panel2MinSize);
    }

    void ResizeStrategyContentPanels()
    {
        var availableHeight = strategyContentSplitter.ClientSize.Height
            - strategyContentSplitter.SplitterWidth;
        if (availableHeight < strategyContentSplitter.Panel1MinSize
            + strategyContentSplitter.Panel2MinSize)
        {
            return;
        }

        strategyContentSplitter.SplitterDistance = Math.Clamp(
            availableHeight / 2,
            strategyContentSplitter.Panel1MinSize,
            availableHeight - strategyContentSplitter.Panel2MinSize);
    }

    void RenderSelectedWorkflow()
    {
        if (_viewModel is null)
            return;
        if (lstStrategyWorkflows.SelectedItems.Count == 0
            || lstStrategyWorkflows.SelectedItems[0].Tag is not StrategyWorkflowRow row)
        {
            _viewModel.Strategy.SelectWorkflow(null);
            txtWorkflowDetails.Text = _viewModel.Strategy.SelectedWorkflowDetails;
            HighlightChartPoint(null);
            return;
        }

        _viewModel.Strategy.SelectWorkflow(row.WorkflowId);
        txtWorkflowDetails.Text = _viewModel.Strategy.SelectedWorkflowDetails;
        HighlightChartPoint(row.TriggerStableIdentity);
    }

    void ConfigureChart()
    {
        itiChart.Font = DashboardTypography.Create();
        var area = new ChartArea("FuturesIti")
        {
            BackColor = Color.Black
        };
        area.AxisX.Title = "Market Time (ET)";
        area.AxisY.Title = "ITI Signal Price";
        area.AxisY.IsStartedFromZero = false;
        foreach (var axis in new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
        {
            axis.LabelStyle.Font = DashboardTypography.Create();
            axis.TitleFont = DashboardTypography.Create();
        }
        foreach (var axis in new[] { area.AxisX, area.AxisY })
        {
            axis.LabelStyle.ForeColor = Color.Silver;
            axis.TitleForeColor = Color.White;
            axis.LineColor = Color.DimGray;
            axis.MajorGrid.LineColor = Color.FromArgb(45, 45, 45);
            axis.MajorTickMark.LineColor = Color.DimGray;
        }
        itiChart.ChartAreas.Add(area);

        itiChart.Legends.Add(new Legend("FuturesItiLegend")
        {
            BackColor = Color.Black,
            ForeColor = Color.White,
            Docking = Docking.Bottom,
            Font = DashboardTypography.Create()
        });
        itiChart.Series.Add(CreateSeries(PriceSeriesName, SeriesChartType.Line, Color.Yellow));
        itiChart.Series.Add(CreateSeries(OtherEventSeriesName, SeriesChartType.Point, Color.Navy));
        itiChart.Series.Add(CreateSeries(UpEventSeriesName, SeriesChartType.Point, Color.LimeGreen));
        itiChart.Series.Add(CreateSeries(DownEventSeriesName, SeriesChartType.Point, Color.Red));
        var selection = CreateSeries(SelectionSeriesName, SeriesChartType.Point, Color.Transparent);
        selection.IsVisibleInLegend = false;
        selection.MarkerStyle = MarkerStyle.Circle;
        selection.MarkerSize = 12;
        selection.MarkerBorderColor = Color.White;
        selection.MarkerBorderWidth = 2;
        itiChart.Series.Add(selection);
    }

    static Series CreateSeries(string name, SeriesChartType chartType, Color color)
        => new(name)
        {
            ChartArea = "FuturesIti",
            ChartType = chartType,
            Color = color,
            XValueType = ChartValueType.DateTime,
            YValueType = ChartValueType.Double,
            BorderWidth = chartType == SeriesChartType.Line ? 2 : 1,
            MarkerStyle = chartType == SeriesChartType.Point ? MarkerStyle.Circle : MarkerStyle.None,
            MarkerSize = 6,
            Font = DashboardTypography.Create()
        };

    void RenderChart(IReadOnlyList<FuturesItiSignalEventRow> events)
    {
        foreach (var series in itiChart.Series)
            series.Points.Clear();

        if (_viewModel is null)
            return;

        var strategy = _viewModel.Strategy;
        ConfigureChartWindow(strategy.ValueDate, strategy.SelectedTimeFrame);

        foreach (var row in events.OrderBy(static row => row.OccurredOn)
                     .ThenBy(static row => row.SequenceId))
        {
            var x = EasternTime.FromUtc(row.OccurredOn).ToOADate();
            AddPoint(itiChart.Series[PriceSeriesName], row, x);
            if (row.Mode == IntrinsicTimeModeType.TrendDirectionChanged)
            {
                var marker = row.Trend == IntrinsicTimeTrendType.UpTrend
                    ? itiChart.Series[UpEventSeriesName]
                    : itiChart.Series[DownEventSeriesName];
                var point = AddPoint(marker, row, x);
                point.MarkerStyle = MarkerStyle.None;
                point.Label = row.Trend == IntrinsicTimeTrendType.UpTrend ? "▲" : "▼";
                point.LabelForeColor = marker.Color;
                point.Font = DashboardTypography.Create(FontStyle.Bold);
            }
            else
            {
                var point = AddPoint(itiChart.Series[OtherEventSeriesName], row, x);
                point.MarkerBorderColor = Color.Silver;
                point.MarkerBorderWidth = 1;
            }
        }

        itiChart.ChartAreas[0].RecalculateAxesScale();
    }

    static DataPoint AddPoint(
        Series series,
        FuturesItiSignalEventRow row,
        double x)
    {
        var index = series.Points.AddXY(x, row.IntrinsicPrice);
        var point = series.Points[index];
        point.Tag = row.StableIdentity;
        point.ToolTip = $"{EasternTime.FromUtc(row.OccurredOn):yyyy-MM-dd hh:mm:ss tt} ET | {row.Mode} | {row.IntrinsicPrice:N2}";
        return point;
    }

    void ConfigureChartWindow(DateOnly valueDate, TimeFrameType timeFrame)
    {
        var window = FuturesItiSignalHistoryWindow.Resolve(valueDate, timeFrame);
        var start = EasternTime.FromUtc(
            FuturesTradingValueDate.GetSessionStartUtc(window.StartValueDate).UtcDateTime);
        var end = EasternTime.FromUtc(
            FuturesTradingValueDate.GetSessionEndUtc(window.EndValueDate).UtcDateTime);
        var axis = itiChart.ChartAreas[0].AxisX;
        axis.Minimum = start.ToOADate();
        axis.Maximum = end.ToOADate();
        axis.LabelStyle.Format = timeFrame == TimeFrameType.Daily
            ? "h:mm tt"
            : timeFrame == TimeFrameType.Weekly
                ? "ddd dd-MMM\nh:mm tt"
                : "dd-MMM";
        axis.IntervalType = timeFrame == TimeFrameType.Daily
            ? DateTimeIntervalType.Hours
            : DateTimeIntervalType.Days;
        axis.Interval = timeFrame switch
        {
            TimeFrameType.Daily => 3,
            TimeFrameType.Weekly => 1,
            _ => 5
        };
    }

    void HighlightChartPoint(string? stableIdentity)
    {
        var selection = itiChart.Series[SelectionSeriesName];
        selection.Points.Clear();
        if (stableIdentity is null)
            return;

        foreach (var series in itiChart.Series.Where(series => series.Name != SelectionSeriesName))
        {
            var point = series.Points.FirstOrDefault(point =>
                string.Equals(point.Tag as string, stableIdentity, StringComparison.Ordinal));
            if (point is null)
                continue;
            selection.Points.AddXY(point.XValue, point.YValues[0]);
            return;
        }
    }

    void itiChart_MouseClick(object? sender, MouseEventArgs e)
    {
        var hit = itiChart.HitTest(e.X, e.Y, false, ChartElementType.DataPoint)
            .FirstOrDefault(result => result.Series is not null && result.PointIndex >= 0);
        if (hit?.Series is null || hit.PointIndex < 0)
            return;
        var stableIdentity = hit.Series.Points[hit.PointIndex].Tag as string;
        if (stableIdentity is null)
            return;

        foreach (ListViewItem item in lstStrategyWorkflows.Items)
        {
            if (item.Tag is not StrategyWorkflowRow row
                || !string.Equals(row.TriggerStableIdentity, stableIdentity, StringComparison.Ordinal))
            {
                continue;
            }

            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            lstStrategyWorkflows.Focus();
            break;
        }
    }

    void ResizeTimeColumnToFit()
    {
        var requiredWidth = TextRenderer.MeasureText(
            colTime.Text,
            lstStrategyWorkflows.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width;
        foreach (ListViewItem item in lstStrategyWorkflows.Items)
        {
            requiredWidth = Math.Max(
                requiredWidth,
                TextRenderer.MeasureText(
                    item.Text,
                    lstStrategyWorkflows.Font,
                    Size.Empty,
                    TextFormatFlags.NoPadding).Width);
        }

        colTime.Width = Math.Max(MinimumTimeColumnWidth, requiredWidth + 16);
    }

    void DrawWorkflowSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        var selected = e.Item.Selected;
        using var background = new SolidBrush(selected ? SystemColors.Highlight : Color.Black);
        e.Graphics.FillRectangle(background, e.Bounds);
        if (e.ColumnIndex == 4 && e.Item.Tag is StrategyWorkflowRow workflow)
        {
            DrawPipelineActors(e.Graphics, e.Bounds, workflow.PipelineActors, selected);
            return;
        }

        var color = selected ? SystemColors.HighlightText : Color.White;
        TextRenderer.DrawText(
            e.Graphics,
            e.SubItem.Text,
            lstStrategyWorkflows.Font,
            Rectangle.Inflate(e.Bounds, -4, 0),
            color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    void DrawPipelineActors(
        Graphics graphics,
        Rectangle bounds,
        IReadOnlyList<PipelineActorIndicator> actors,
        bool selected)
    {
        var x = bounds.Left + 5;
        const int diameter = 12;
        foreach (var actor in actors)
        {
            var circle = new Rectangle(x, bounds.Top + Math.Max(1, (bounds.Height - diameter) / 2), diameter, diameter);
            using var brush = new SolidBrush(PipelineColor(actor.DisplayState));
            graphics.FillEllipse(brush, circle);
            using var outline = new Pen(selected ? Color.White : Color.DimGray);
            graphics.DrawEllipse(outline, circle);
            x += diameter + 3;
            var labelWidth = TextRenderer.MeasureText(actor.ShortLabel, lstStrategyWorkflows.Font).Width;
            TextRenderer.DrawText(
                graphics,
                actor.ShortLabel,
                lstStrategyWorkflows.Font,
                new Rectangle(x, bounds.Top, labelWidth, bounds.Height),
                selected ? SystemColors.HighlightText : Color.LightGray,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            x += labelWidth + 7;
        }
    }

    internal static Color PipelineColor(PipelineActorDisplayState state) => state switch
    {
        PipelineActorDisplayState.Processing => Color.Yellow,
        PipelineActorDisplayState.Continued => Color.Lime,
        PipelineActorDisplayState.Stopped => Color.Red,
        _ => Color.Red
    };

    static string FormatListTime(DateTime occurredOn, TimeFrameType timeFrame)
    {
        var format = timeFrame == TimeFrameType.Daily
            ? "hh:mm:ss.fff tt"
            : "dd-MMM-yyyy hh:mm:ss.fff tt";
        return EasternTime.FromUtc(occurredOn)
            .ToString(format, CultureInfo.InvariantCulture);
    }
}
