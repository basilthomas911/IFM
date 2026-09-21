using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>
/// market data view for displaying futures bar data.
/// </summary>
public partial class MarketDataView : DarkTradingView
{
    public MarketDataView()
    {
        InitializeComponent();
        DashboardTypography.ApplyFamilyAndSize(this);
        ConfigureChartGrid(graphES);
        ConfigureChartGrid(graphEsBollinger);
        ConfigureChartGrid(graphVIX);
    }

    static void ConfigureChartGrid(Chart chart)
    {
        chart.Font = DashboardTypography.Create();
        foreach (var area in chart.ChartAreas)
        {
            foreach (var axis in new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
            {
                axis.LabelStyle.Font = DashboardTypography.Create();
                axis.TitleFont = DashboardTypography.Create();
            }
            foreach (var axis in new[] { area.AxisX, area.AxisY2 })
            {
                axis.LineColor = Color.DimGray;
                axis.MajorGrid.Enabled = true;
                axis.MajorGrid.LineColor = Color.FromArgb(45, 45, 45);
                axis.MajorTickMark.LineColor = Color.DimGray;
            }
        }
        foreach (var legend in chart.Legends)
            legend.Font = DashboardTypography.Create();
        foreach (var title in chart.Titles)
            title.Font = DashboardTypography.Create(FontStyle.Bold);
        foreach (var series in chart.Series)
            series.Font = DashboardTypography.Create();
    }

    /// <summary>Refreshes the 40-day daily ES Bollinger Band chart.</summary>
    /// <param name="snapshot">The selected value date and ordered daily Bollinger observations.</param>
    public bool RefreshView(FuturesBollingerBandChartSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Signals.Length == 0)
            return false;

        var signals = snapshot.Signals
            .OrderBy(signal => signal.Metadata.ValueDate)
            .ToArray();
        var plottedValues = signals
            .SelectMany(signal => new decimal?[]
            {
                signal.Price,
                signal.Ema20Center,
                signal.Upper20,
                signal.Lower20
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        if (plottedValues.Length == 0)
            return false;

        graphEsBollinger.AccessibleName = "ES daily Bollinger Band chart";
        graphEsBollinger.AccessibleDescription =
            $"{signals.Length} daily ES Bollinger observation(s) ending {snapshot.ValueDate:yyyy-MM-dd}";
        graphEsBollinger.SuspendLayout();
        try
        {
            var area = graphEsBollinger.ChartAreas[0];
            area.AxisY2.Interval = 0;
            area.AxisY2.IntervalType = DateTimeIntervalType.Number;
            area.AxisY2.LabelStyle.Format = "N0";
            area.AxisY2.IsStartedFromZero = false;
            var minimum = Convert.ToDouble(plottedValues.Min());
            var maximum = Convert.ToDouble(plottedValues.Max());
            var padding = Math.Max(1d, (maximum - minimum) * 0.05d);
            area.AxisY2.Minimum = minimum - padding;
            area.AxisY2.Maximum = maximum + padding;
            area.AxisX.ScaleView.ZoomReset(0);
            area.AxisX.LabelStyle.Format = "MMM d";
            area.AxisX.IntervalType = DateTimeIntervalType.Days;
            area.AxisX.Interval = Math.Max(1d, Math.Ceiling(signals.Length / 8d));

            foreach (var series in graphEsBollinger.Series)
                series.Points.Clear();

            foreach (var signal in signals)
            {
                var date = signal.Metadata.ValueDate.ToDateTime(TimeOnly.MinValue);
                graphEsBollinger.Series["ES Close"].Points.AddXY(date, signal.Price);
                AddOptionalPoint(graphEsBollinger.Series["20 EMA"], date, signal.Ema20Center);
                AddOptionalPoint(graphEsBollinger.Series["Upper Band"], date, signal.Upper20);
                AddOptionalPoint(graphEsBollinger.Series["Lower Band"], date, signal.Lower20);
            }

            var firstDate = signals[0].Metadata.ValueDate.ToDateTime(TimeOnly.MinValue);
            var lastDate = snapshot.ValueDate.ToDateTime(TimeOnly.MinValue);
            if (lastDate <= firstDate)
                lastDate = firstDate.AddDays(1);
            area.AxisX.Minimum = firstDate.ToOADate();
            area.AxisX.Maximum = lastDate.ToOADate();
            area.RecalculateAxesScale();
            graphEsBollinger.Update();
            return true;
        }
        finally
        {
            graphEsBollinger.ResumeLayout();
        }
    }

    static void AddOptionalPoint(Series series, DateTime date, decimal? value)
    {
        if (value.HasValue)
            series.Points.AddXY(date, value.Value);
    }

    /// <summary>
    /// Refreshes the view with every futures bar in the snapshot's fixed wall-clock window.
    /// </summary>
    /// <param name="snapshot">The symbol, six-hour UTC window, and persisted bars to render.</param>
    public bool RefreshView(FuturesBarChartSnapshot snapshot)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var symbol = snapshot.Symbol;
            var futuresBarData = snapshot.Bars;
            if (futuresBarData?.Length  == 0) 
                return false;
            var graph = default(Chart);
            var minMaxOffset = 0.0;
            switch (symbol)
            {
                case "ES":
                    graph = graphES;
                    minMaxOffset = 5;
                    break;
                case "VX":
                    graph = graphVIX;
                    minMaxOffset = 0.025;
                    break;
                default:
                    return false;
            }
            graph.AccessibleName = $"{symbol} futures bar chart";
            graph.AccessibleDescription = $"{futuresBarData.Length} futures bar data point(s)";
            var upTrendTrigger = Convert.ToDecimal(futuresBarData?.LastOrDefault()?.UpTrendTrigger ?? 0);
            var downTrendTrigger = Convert.ToDecimal(futuresBarData?.LastOrDefault()?.DownTrendTrigger ?? 0);
            var maximum = upTrendTrigger > 0
                ? Convert.ToDouble(Math.Max(futuresBarData?.Max(e => e.BarValue) ?? 0, upTrendTrigger))
                : Convert.ToDouble(futuresBarData?.Max(e => e.BarValue) ?? 0);

            var minimum = downTrendTrigger > 0
                ? Convert.ToDouble(Math.Min(futuresBarData?.Min(e => e.BarValue) ?? 0, downTrendTrigger))
                : Convert.ToDouble(futuresBarData?.Min(e => e.BarValue) ?? 0);

            graph.SuspendLayout();
            graph.ChartAreas[0].AxisY2.Interval = 0.0;
            graph.ChartAreas[0].AxisY2.IntervalType = DateTimeIntervalType.Number;
            graph.ChartAreas[0].AxisY2.IsStartedFromZero = false;
            var displayedMinimum = symbol == "ES"
                ? minimum
                : Convert.ToDouble(futuresBarData.Min(e => e.BarValue));
            var displayedMaximum = symbol == "ES"
                ? maximum
                : Convert.ToDouble(futuresBarData.Max(e => e.BarValue));
            graph.ChartAreas[0].AxisY2.Minimum = displayedMinimum - minMaxOffset;
            graph.ChartAreas[0].AxisY2.Maximum = displayedMaximum + minMaxOffset;
            var marketBarDates = futuresBarData
                .Select(e => EasternTime.FromUtc(e.BarDate))
                .ToArray();
            var windowStart = EasternTime.FromUtc(snapshot.WindowStartUtc);
            var windowEnd = EasternTime.FromUtc(snapshot.WindowEndUtc);
            graph.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            graph.ChartAreas[0].AxisX.Minimum = windowStart.ToOADate();
            graph.ChartAreas[0].AxisX.Maximum = windowEnd.ToOADate();
            graph.ChartAreas[0].AxisX.LabelStyle.Format = "h:mm:ss tt";
            graph.Series[0].Points.Clear();
            var extendSingleObservation = futuresBarData.Length == 1
                && marketBarDates[0] < windowEnd;
            graph.Series[0].MarkerStyle = extendSingleObservation
                ? MarkerStyle.None
                : futuresBarData.Length == 1
                ? MarkerStyle.Circle
                : MarkerStyle.None;
            graph.Series[0].MarkerSize = 6;
            if (graph.Series.Count > 1)
            {
                graph.Series[1].Points.Clear();
                graph.Series[2].Points.Clear();
            }
            for (var index = 0; index < futuresBarData.Length; index++)
            {
                var e = futuresBarData[index];
                var marketBarDate = marketBarDates[index];
                graph.Series[0].Points.AddXY(marketBarDate, e.BarValue);
                if (graph.Series.Count > 1)
                {
                    graph.Series[1].Points.AddXY(marketBarDate, upTrendTrigger);
                    graph.Series[2].Points.AddXY(marketBarDate, downTrendTrigger);
                }
            }
            if (extendSingleObservation)
            {
                var onlyBar = futuresBarData[0];
                graph.Series[0].Points.AddXY(windowEnd, onlyBar.BarValue);
                if (graph.Series.Count > 1)
                {
                    graph.Series[1].Points.AddXY(windowEnd, upTrendTrigger);
                    graph.Series[2].Points.AddXY(windowEnd, downTrendTrigger);
                }
            }
            graph.ChartAreas[0].RecalculateAxesScale();
            graph.Update();
            graph.ResumeLayout();
            return true;
        }
        catch { return false; }
    }

}
