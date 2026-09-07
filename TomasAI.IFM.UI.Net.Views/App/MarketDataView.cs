using System.Windows.Forms.DataVisualization.Charting;
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

    /// <summary>
    /// Refreshes the view with every futures bar in the snapshot's fixed wall-clock window.
    /// </summary>
    /// <param name="snapshot">The symbol, six-hour UTC window, and persisted bars to render.</param>
    public void RefreshView(FuturesBarChartSnapshot snapshot)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var symbol = snapshot.Symbol;
            var futuresBarData = snapshot.Bars;
            if (futuresBarData?.Length  == 0) 
                return;
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
                    return;
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
        }
        catch {  }
    }

}
