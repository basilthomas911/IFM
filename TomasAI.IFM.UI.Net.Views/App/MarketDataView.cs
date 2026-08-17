using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>
/// market data view for displaying futures bar data.
/// </summary>
public partial class MarketDataView : UserControl
{
    public MarketDataView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// refreshes the view with the latest futures bar data for the specified symbol.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="futuresBarData"></param>
    public void RefreshView(string symbol, FuturesBarDataReadModel[] futuresBarData)
    {
        try
        {
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
            var earliestBarDate = marketBarDates.Min();
            var latestBarDate = marketBarDates.Max();
            if (earliestBarDate == latestBarDate)
            {
                earliestBarDate = earliestBarDate.AddSeconds(-7.5);
                latestBarDate = latestBarDate.AddSeconds(7.5);
            }
            graph.ChartAreas[0].AxisX.ScaleView.ZoomReset(0);
            graph.ChartAreas[0].AxisX.Minimum = earliestBarDate.ToOADate();
            graph.ChartAreas[0].AxisX.Maximum = latestBarDate.ToOADate();
            graph.ChartAreas[0].AxisX.LabelStyle.Format = "h:mm:ss tt";
            graph.Series[0].Points.Clear();
            graph.Series[0].MarkerStyle = futuresBarData.Length == 1
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
            graph.ChartAreas[0].RecalculateAxesScale();
            graph.Update();
            graph.ResumeLayout();
        }
        catch {  }
    }

}
