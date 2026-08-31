using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.App
{
    public partial class MarketOutlookView : UserControl
    {
        readonly Label _snapshotStatus = new()
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(32, 32, 32),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0),
            Text = "Market Outlook: no persisted snapshot"
        };

        public MarketOutlookView()
        {
            try
            {
                InitializeComponent();
                tlpMarketOutlook.RowCount = 3;
                tlpMarketOutlook.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                tlpMarketOutlook.Controls.Add(_snapshotStatus, 0, 2);
                tlpMarketOutlook.SetColumnSpan(_snapshotStatus, 4);
                ConfigureMarketDataRowSpacing();
                txtRSI.Text = "No";
                txtRSI.BackColor = Color.Red;
                ConfigureAccessibility();
                //txt50DMA.BackColor = Color.Black;
                //txt200DMA.BackColor = Color.Black;
            }
            catch { }
        }

        void ConfigureMarketDataRowSpacing()
        {
            foreach (var valueControl in tlpMarketData.Controls.OfType<TextBox>())
            {
                valueControl.Margin = new Padding(
                    valueControl.Margin.Left,
                    2,
                    valueControl.Margin.Right,
                    2);
            }
        }

        public void RefreshView(FuturesEodDataUIViewModel e)
        {
            txtMarketTrendRT.Text = e.MarketDirection;
            txtMarketTrendRT.ForeColor = e.MarketDirectionForeColor.ToColor();
            txtMarketTrendRT.BackColor = e.MarketDirectionBackColor.ToColor();
            txtMarketVolatilityRT.Text = e.MarketVolatility;
            txtMarketVolatilityRT.ForeColor = e.MarketVolatilityForeColor.ToColor();
            txtMarketVolatilityRT.BackColor = e.MarketVolatilityBackColor.ToColor();
            txtMarketDirectionRT.Text = e.PriceDirection;
            txtMarketDirectionRT.ForeColor = e.PriceDirectionForeColor.ToColor();
            txtMarketDirectionRT.BackColor = e.PriceDirectionBackColor.ToColor();
            txtVixVolRT.Text = e.PriceVolatility;
            txtVixVolRT.ForeColor = e.PriceVolatilityForeColor.ToColor();
            txtVixVolRT.BackColor = e.PriceVolatilityBackColor.ToColor();
            txtOpenRT.Text = e.OpenPrice;
            txtHighRT.Text = e.HighPrice;
            txtLowRT.Text = e.LowPrice;
            txtCloseRT.Text = e.ClosePrice;
            txtVolumeRT.Text = e.Volume;
            txtPercentChangeRT.Text = e.DailyPercentChange;
            txtPercentChangeRT.ForeColor = e.DailyPercentChangeForeColor.ToColor();
            txtPercentChangeRT.BackColor = e.DailyPercentChangeBackColor.ToColor();
            txtStdDevRT.Text = e.DailyStdDev;
            txtUpperBandRT.Text = e.UpperBand;
            txtMeanRT.Text = e.Mean;
            txtLowerBandRT.Text = e.LowerBand;
            txtMDI.Text = e.MDI;
            txtMDI.ForeColor = e.MDIForeColor.ToColor();
            txtMDI.BackColor = e.MDIBackColor.ToColor();
            UpdateMarketOutlookAccessibility();
        }

        void ConfigureAccessibility()
        {
            txtMarketTrendRT.AccessibleName = "Market direction";
            txtMarketVolatilityRT.AccessibleName = "Market volatility";
            txtMarketDirectionRT.AccessibleName = "Price direction";
            txtVixVolRT.AccessibleName = "Price volatility";
            txtOpenRT.AccessibleName = "Open price";
            txtHighRT.AccessibleName = "High price";
            txtLowRT.AccessibleName = "Low price";
            txtCloseRT.AccessibleName = "Close price";
            txtVolumeRT.AccessibleName = "Volume";
            txtPercentChangeRT.AccessibleName = "Daily percent change";
        }

        void UpdateMarketOutlookAccessibility()
        {
            foreach (var control in new[]
                     {
                         txtMarketTrendRT, txtMarketVolatilityRT, txtMarketDirectionRT, txtVixVolRT,
                         txtOpenRT, txtHighRT, txtLowRT, txtCloseRT, txtVolumeRT, txtPercentChangeRT
                     })
            {
                control.AccessibleDescription = control.Text;
            }
        }

        public void RefreshView(FuturesTradeSignalUIViewModel e)
        {
            txt50DMA.Text = e.FiftyDMA;
            txt200DMA.Text = e.TwoHundredDMA;

            txtTrend.Text = e.Trend;
            txtTrend.ForeColor = e.TrendForeColor.ToColor();
            txtTrend.BackColor = e.TrendBackColor.ToColor();
            txtMDITrend.Text = e.MDITrend;
            txtMDITrend.ForeColor = e.MDITrendForeColor.ToColor();
            txtMDITrend.BackColor = e.MDITrendBackColor.ToColor();
            txtMDIUpLimit.Text = e.MDIUpLimit;
            txtMDIUpLimit.ForeColor = e.MDIUpLimitForeColor.ToColor();
            txtMDIUpLimit.BackColor = e.MDIUpLimitBackColor.ToColor();
            txtMDIDownLimit.Text = e.MDIDownLimit;
            txtMDIDownLimit.ForeColor = e.MDIDownLimitForeColor.ToColor();
            txtMDIDownLimit.BackColor = e.MDIDownLimitBackColor.ToColor();
            txtRSI.Text = e.RSI;
            txtRSI.ForeColor = e.RSIForeColor.ToColor();
            txtRSI.BackColor = e.RSIBackColor.ToColor();

            txtUpTrendLimit.Text = e.UpTrendLimit;
            txtUpTrendLimit.ForeColor = e.UpTrendLimitForeColor.ToColor();
            txtDownTrendLimit.Text = e.DownLimitTrigger;
            txtDownTrendLimit.ForeColor = e.DownLimitTriggerForeColor.ToColor();
            txtExtremeLimit.Text = e.TrendExtreme;
            txtExtremeLimit.ForeColor = e.TrendExtremeForeColor.ToColor();
            txtReversalLimit.Text = e.TrendReversal;
            txtReversalLimit.ForeColor = e.TrendReversalForeColor.ToColor();
            txtTrendDelta.Text = e.TrendDelta;
            txtTrendDelta.ForeColor = e.TrendDeltaForeColor.ToColor();
        }

        public void RefreshView(PlaceTradeUIViewModel e)
        {
        }

        public void RefreshSnapshotStatus(string status)
        {
            _snapshotStatus.Text = string.IsNullOrWhiteSpace(status)
                ? "Market Outlook: no persisted snapshot"
                : status;
            _snapshotStatus.AccessibleName = "Market Outlook snapshot status";
            _snapshotStatus.AccessibleDescription = _snapshotStatus.Text;
        }

        public void ResizeView(Control parentControl)
        {
            this.Width = parentControl.Width;
            this.Height = parentControl.Height;
            tlpMarketOutlook.Height = 75;
            tlpMarketOutlook.Controls[0].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[1].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[2].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[3].Width = parentControl.Width / 4;
            const int rowBreathingRoom = 1;
            var marketDataRowHeight = tlpMarketData.Controls
                .Cast<Control>()
                .Max(control => control.PreferredSize.Height + control.Margin.Vertical) + rowBreathingRoom;
            foreach (RowStyle rowStyle in tlpMarketData.RowStyles)
            {
                rowStyle.SizeType = SizeType.Absolute;
                rowStyle.Height = marketDataRowHeight;
            }
            tlpMarketData.Height = marketDataRowHeight * tlpMarketData.RowCount;
            var marketTrendRowHeight = tlpMarketTrendData.Controls
                .Cast<Control>()
                .Max(control => control.PreferredSize.Height + control.Margin.Vertical) + rowBreathingRoom;
            foreach (RowStyle rowStyle in tlpMarketTrendData.RowStyles)
            {
                rowStyle.SizeType = SizeType.Absolute;
                rowStyle.Height = marketTrendRowHeight;
            }
            tlpMarketTrendData.Height = marketTrendRowHeight * tlpMarketTrendData.RowCount;
            parentControl.Height = tlpMarketOutlook.Height + tlpMarketData.Height + tlpMarketTrendData.Height + 12;
        }

        private void lblRiskPosition_Click(object sender, EventArgs e)
        {

        }

        private void lblMarketTrendRT_Click(object sender, EventArgs e)
        {

        }

        private void txt200DMA_TextChanged(object sender, EventArgs e)
        {

        }

        private void lblTdiStrength_Click(object sender, EventArgs e)
        {

        }
    }
}
