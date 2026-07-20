using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Views.App
{
    public partial class MarketDataView : UserControl
    {
        public MarketDataView()
        {
            InitializeComponent();
        }

        public void RefreshView(string symbol, FuturesBarDataReadModel[] futuresBarData)
        {
            try
            {
                if ((futuresBarData?.Length ?? 0) == 0) return;
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
                var upTrendTrigger = Convert.ToDecimal(futuresBarData.LastOrDefault()?.UpTrendTrigger ?? 0);
                var downTrendTrigger = Convert.ToDecimal(futuresBarData.LastOrDefault()?.DownTrendTrigger ?? 0);
                var maximum = upTrendTrigger > 0
                    ? Convert.ToDouble(Math.Max(futuresBarData.Max(e => e.BarValue), upTrendTrigger))
                    : Convert.ToDouble(futuresBarData.Max(e => e.BarValue));
                
                var minimum = downTrendTrigger > 0
                    ? Convert.ToDouble(Math.Min(futuresBarData.Min(e => e.BarValue), downTrendTrigger))
                    : Convert.ToDouble(futuresBarData.Min(e => e.BarValue));

                graph.SuspendLayout();
                graph.ChartAreas[0].AxisY2.Interval = 0.0;
                graph.ChartAreas[0].AxisY2.IntervalType = DateTimeIntervalType.Number;
                graph.ChartAreas[0].AxisY2.IsStartedFromZero = false;
                graph.ChartAreas[0].AxisY2.Minimum = Convert.ToInt32( (symbol == "ES" ? minimum : Convert.ToDouble(futuresBarData.Min(e => e.BarValue)))  - minMaxOffset );
                graph.ChartAreas[0].AxisY2.Maximum = Convert.ToInt32( (symbol == "ES" ? maximum : Convert.ToDouble(futuresBarData.Max(e => e.BarValue))) + minMaxOffset );
                graph.Series[0].Points.Clear();
                if (graph.Series.Count > 1)
                {
                    graph.Series[1].Points.Clear();
                    graph.Series[2].Points.Clear();
                }
                foreach (var e in futuresBarData)
                {
                    graph.Series[0].Points.AddXY(e.BarDate, e.BarValue);
                    if (graph.Series.Count > 1)
                    {
                        graph.Series[1].Points.AddXY(e.BarDate, upTrendTrigger);
                        graph.Series[2].Points.AddXY(e.BarDate, downTrendTrigger);
                    }
                }
                graph.ChartAreas[0].RecalculateAxesScale();
                graph.Update();
                graph.ResumeLayout();
            }
            catch {  }
        }
    }
}
