using System.ComponentModel;
using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Net.Extensions;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>WinForms adapter for the framework-neutral Operations presentation state.</summary>
public partial class OperationsView : UserControl
{
    const double StrategyDetailHeightRatio = 0.33;
    const int MinimumTimeColumnWidth = 185;
    OperationsViewModel? _viewModel;
    IReadOnlyList<FuturesItiSignalEventRow>? _renderedEvents;
    bool _synchronizingSelection;
    bool _synchronizingTimeFrame;

    public OperationsView()
    {
        InitializeComponent();
        lstItiEvents.SetDoubleBuffered(true);
        ddlTimeFrame.Items.AddRange(
            [TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly]);
        ddlTimeFrame.SelectedItem = TimeFrameType.Daily;
        operationsTabs.SelectedIndex = (int)OperationsViewType.Strategy;
        ResizeStrategyPanels();
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

        lblItiStatus.Text = strategy.LastError is null
            ? strategy.StatusText
            : $"{strategy.StatusText} | {strategy.LastError.Caption}";
        lblItiStatus.ForeColor = strategy.LastError is not null
            ? Color.Gold
            : strategy.IsListening ? Color.LimeGreen : Color.Silver;

        if (!ReferenceEquals(_renderedEvents, strategy.Events))
        {
            RenderEvents(strategy.Events);
            _renderedEvents = strategy.Events;
        }
    }

    void RenderEvents(IReadOnlyList<FuturesItiSignalEventRow> events)
    {
        var selectedIdentity = lstItiEvents.SelectedItems.Count == 0
            ? null
            : (lstItiEvents.SelectedItems[0].Tag as FuturesItiSignalEventRow)?.StableIdentity;

        lstItiEvents.BeginUpdate();
        try
        {
            lstItiEvents.Items.Clear();
            foreach (var row in events)
            {
                var item = new ListViewItem(FormatListTime(row.OccurredOn, row.TimePeriod))
                {
                    Tag = row
                };
                item.SubItems.Add(row.Mode.ToStringFast());
                item.SubItems.Add(row.Trend.ToStringFast());
                item.SubItems.Add(row.IntrinsicPrice.ToString("N2", CultureInfo.InvariantCulture));
                lstItiEvents.Items.Add(item);
                if (row.StableIdentity == selectedIdentity)
                    item.Selected = true;
            }
        }
        finally
        {
            lstItiEvents.EndUpdate();
        }

        ResizeTimeColumnToFit();
        if (lstItiEvents.SelectedItems.Count == 0 && lstItiEvents.Items.Count > 0)
            lstItiEvents.Items[0].Selected = true;
        RenderSelectedEvent();
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

    void lstItiEvents_SelectedIndexChanged(object? sender, EventArgs e)
        => RenderSelectedEvent();

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

    void RenderSelectedEvent()
    {
        if (lstItiEvents.SelectedItems.Count == 0
            || lstItiEvents.SelectedItems[0].Tag is not FuturesItiSignalEventRow row)
        {
            itiPropertyGrid.SelectedObject = null;
            return;
        }

        itiPropertyGrid.SelectedObject = new ItiSignalPropertyGridModel(row);
    }

    void ResizeTimeColumnToFit()
    {
        var requiredWidth = TextRenderer.MeasureText(
            colTime.Text,
            lstItiEvents.Font,
            Size.Empty,
            TextFormatFlags.NoPadding).Width;
        foreach (ListViewItem item in lstItiEvents.Items)
        {
            requiredWidth = Math.Max(
                requiredWidth,
                TextRenderer.MeasureText(
                    item.Text,
                    lstItiEvents.Font,
                    Size.Empty,
                    TextFormatFlags.NoPadding).Width);
        }

        colTime.Width = Math.Max(MinimumTimeColumnWidth, requiredWidth + 16);
    }

    static string FormatListTime(DateTime occurredOn, TimeFrameType timeFrame)
    {
        var format = timeFrame == TimeFrameType.Daily
            ? "hh:mm:ss.fff tt"
            : "dd-MMM-yyyy hh:mm:ss.fff tt";
        return EasternTime.FromUtc(occurredOn)
            .ToString(format, CultureInfo.InvariantCulture);
    }
}

sealed class ItiSignalPropertyGridModel
{
    public ItiSignalPropertyGridModel(FuturesItiSignalEventRow row)
    {
        Contract = row.ContractId;
        ValueDate = row.ValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TimePeriod = row.TimePeriod.ToStringFast();
        Sequence = row.SequenceId.ToString(CultureInfo.InvariantCulture);
        Occurred = FormatTime(row.OccurredOn);
        Received = FormatTime(row.ReceivedOn);
        Mode = row.Mode.ToStringFast();
        Trend = row.Trend.ToStringFast();
        TradeState = row.TradeState.ToStringFast();
        IntrinsicPrice = FormatPrice(row.IntrinsicPrice);
        IntrinsicTimeGroup = row.IntrinsicTimeGroupId.ToString(CultureInfo.InvariantCulture);
        IntrinsicTimeLength = row.IntrinsicTimeLength.ToString("N2", CultureInfo.InvariantCulture);
        TrendPrice = FormatPrice(row.TrendPrice);
        TrendExtreme = FormatPrice(row.TrendExtreme);
        TrendReversal = FormatPrice(row.TrendReversal);
        TrendDelta = FormatPrice(row.TrendDelta);
        TargetDelta = FormatPrice(row.TargetDelta);
        Threshold = row.Threshold.ToString("N4", CultureInfo.InvariantCulture);
        UpTrendTrigger = FormatPrice(row.UpTrendTrigger);
        DownTrendTrigger = FormatPrice(row.DownTrendTrigger);
        TimeFrameStart = row.TimeFrameStartValueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Source = row.IsInitialSnapshot ? "Startup snapshot" : "Notification";
        NotificationId = row.NotificationId.ToString();
        SourceEventId = row.SourceEventId.ToString();
        EventId = row.EventId.ToString(CultureInfo.InvariantCulture);
        CommandId = row.CommandId.ToString();
    }

    [DisplayName("Contract")]
    public string Contract { get; }

    [DisplayName("Value Date")]
    public string ValueDate { get; }

    [DisplayName("Time Period")]
    public string TimePeriod { get; }

    [DisplayName("Sequence")]
    public string Sequence { get; }

    [DisplayName("Occurred (ET)")]
    public string Occurred { get; }

    [DisplayName("Received (ET)")]
    public string Received { get; }

    [DisplayName("Mode")]
    public string Mode { get; }

    [DisplayName("Trend")]
    public string Trend { get; }

    [DisplayName("Trade State")]
    public string TradeState { get; }

    [DisplayName("Intrinsic Price")]
    public string IntrinsicPrice { get; }

    [DisplayName("ITI Group")]
    public string IntrinsicTimeGroup { get; }

    [DisplayName("ITI Length")]
    public string IntrinsicTimeLength { get; }

    [DisplayName("Trend Price")]
    public string TrendPrice { get; }

    [DisplayName("Trend Extreme")]
    public string TrendExtreme { get; }

    [DisplayName("Trend Reversal")]
    public string TrendReversal { get; }

    [DisplayName("Trend Delta")]
    public string TrendDelta { get; }

    [DisplayName("Target Delta")]
    public string TargetDelta { get; }

    [DisplayName("Threshold")]
    public string Threshold { get; }

    [DisplayName("Up Trend Trigger")]
    public string UpTrendTrigger { get; }

    [DisplayName("Down Trend Trigger")]
    public string DownTrendTrigger { get; }

    [DisplayName("Time Frame Start")]
    public string TimeFrameStart { get; }

    [DisplayName("Source")]
    public string Source { get; }

    [DisplayName("Notification ID")]
    public string NotificationId { get; }

    [DisplayName("Source Event ID")]
    public string SourceEventId { get; }

    [DisplayName("Event ID")]
    public string EventId { get; }

    [DisplayName("Command ID")]
    public string CommandId { get; }

    static string FormatTime(DateTime value)
        => EasternTime.FromUtc(value)
            .ToString("yyyy-MM-dd hh:mm:ss.fff tt", CultureInfo.InvariantCulture);

    static string FormatPrice(double value)
        => value.ToString("N2", CultureInfo.InvariantCulture);
}
