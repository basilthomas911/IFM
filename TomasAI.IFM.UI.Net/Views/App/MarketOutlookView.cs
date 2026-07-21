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

namespace TomasAI.IFM.UI.Net.Views.App
{
    public partial class MarketOutlookView : UserControl
    {
        public MarketOutlookView()
        {
            try
            {
                InitializeComponent();
                txtRSI.Text = "No";
                txtRSI.BackColor = Color.Red;
                //txt50DMA.BackColor = Color.Black;
                //txt200DMA.BackColor = Color.Black;
            }
            catch { }
        }

        public void RefreshView(FuturesEodDataUIViewModel e)
        {
            txtMarketTrendRT.Text = e.MarketDirection;
            txtMarketTrendRT.ForeColor = e.MarketDirectionForeColor;
            txtMarketTrendRT.BackColor = e.MarketDirectionBackColor;
            txtMarketVolatilityRT.Text = e.MarketVolatility;
            txtMarketVolatilityRT.ForeColor = e.MarketVolatilityForeColor;
            txtMarketVolatilityRT.BackColor = e.MarketVolatilityBackColor;
            txtMarketDirectionRT.Text = e.PriceDirection;
            txtMarketDirectionRT.ForeColor = e.PriceDirectionForeColor;
            txtMarketDirectionRT.BackColor = e.PriceDirectionBackColor;
            txtVixVolRT.Text = e.PriceVolatility;
            txtVixVolRT.ForeColor = e.PriceVolatilityForeColor;
            txtVixVolRT.BackColor = e.PriceVolatilityBackColor;
            txtOpenRT.Text = e.OpenPrice;
            txtHighRT.Text = e.HighPrice;
            txtLowRT.Text = e.LowPrice;
            txtCloseRT.Text = e.ClosePrice;
            txtVolumeRT.Text = e.Volume;
            txtPercentChangeRT.Text = e.DailyPercentChange;
            txtPercentChangeRT.ForeColor = e.DailyPercentChangeForeColor;
            txtPercentChangeRT.BackColor = e.DailyPercentChangeBackColor;
            txtStdDevRT.Text = e.DailyStdDev;
            txtUpperBandRT.Text = e.UpperBand;
            txtMeanRT.Text = e.Mean;
            txtLowerBandRT.Text = e.LowerBand;
            txtMDI.Text = e.MDI;
            txtMDI.ForeColor = e.MDIForeColor;
            txtMDI.BackColor = e.MDIBackColor;
        }

        public void RefreshView(FuturesTradeSignalUIViewModel e)
        {
            txt50DMA.Text = e.FiftyDMA;
            txt200DMA.Text = e.TwoHundredDMA;

            txtTrend.Text = e.Trend;
            txtTrend.ForeColor = e.TrendForeColor;
            txtTrend.BackColor = e.TrendBackColor;
            txtMDITrend.Text = e.MDITrend;
            txtMDITrend.ForeColor = e.MDITrendForeColor;
            txtMDITrend.BackColor = e.MDITrendBackColor;
            txtMDIUpLimit.Text = e.MDIUpLimit;
            txtMDIUpLimit.ForeColor = e.MDIUpLimitForeColor;
            txtMDIUpLimit.BackColor = e.MDIUpLimitBackColor;
            txtMDIDownLimit.Text = e.MDIDownLimit;
            txtMDIDownLimit.ForeColor = e.MDIDownLimitForeColor;
            txtMDIDownLimit.BackColor = e.MDIDownLimitBackColor;
            txtRSI.Text = e.RSI;
            txtRSI.ForeColor = e.RSIForeColor;
            txtRSI.BackColor = e.RSIBackColor;

            txtUpTrendLimit.Text = e.UpTrendLimit;
            txtUpTrendLimit.ForeColor = e.UpTrendLimitForeColor;
            txtDownTrendLimit.Text = e.DownLimitTrigger;
            txtDownTrendLimit.ForeColor = e.DownLimitTriggerForeColor;
            txtExtremeLimit.Text = e.TrendExtreme;
            txtExtremeLimit.ForeColor = e.TrendExtremeForeColor;
            txtReversalLimit.Text = e.TrendReversal;
            txtReversalLimit.ForeColor = e.TrendReversalForeColor;
            txtTrendDelta.Text = e.TrendDelta;
            txtTrendDelta.ForeColor = e.TrendDeltaForeColor;
        }

        public void RefreshView(PlaceTradeUIViewModel e)
        {
        }

        public void ResizeView(Control parentControl)
        {
            this.Width = parentControl.Width;
            this.Height = parentControl.Height;
            tlpMarketOutlook.Height = 55;
            tlpMarketOutlook.Controls[0].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[1].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[2].Width = parentControl.Width / 4;
            tlpMarketOutlook.Controls[3].Width = parentControl.Width / 4;
            tlpMarketData.Height = 130;
            tlpMarketTrendData.Height = 55;
            parentControl.Height = tlpMarketOutlook.Height + tlpMarketData.Height + tlpMarketTrendData.Height + 10;
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
