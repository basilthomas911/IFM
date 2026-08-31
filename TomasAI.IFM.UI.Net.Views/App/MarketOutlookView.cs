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
        const int CompactBottomClearance = 6;

        readonly TableLayoutPanel _tdiData = new();
        readonly TextBox _txtTdiDirection = CreateTdiValue();
        readonly TextBox _txtTdiStrength = CreateTdiValue();
        readonly TextBox _txtTdiMarketState = CreateTdiValue();
        readonly TextBox _txtTdiCross = CreateTdiValue();
        readonly TextBox _txtTdiDivergence = CreateTdiValue();
        readonly Label _snapshotStatus = new()
        {
            Name = "lblMarketOutlookSnapshotStatus",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(32, 32, 32),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0),
            Font = DashboardTypography.Create(),
            Text = "Market Outlook: no persisted snapshot"
        };

        public MarketOutlookView()
        {
            try
            {
                InitializeComponent();
                ConfigureTypographyAndAlignment();
                ConfigureTdiRow();
                tlpMarketOutlook.RowCount = 3;
                tlpMarketOutlook.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
                tlpMarketOutlook.Controls.Add(_snapshotStatus, 0, 2);
                tlpMarketOutlook.SetColumnSpan(_snapshotStatus, 4);
                DashboardTypography.ApplyFamilyAndSize(this);
                txtRSI.Text = "No";
                txtRSI.BackColor = Color.Red;
                ConfigureAccessibility();
                //txt50DMA.BackColor = Color.Black;
                //txt200DMA.BackColor = Color.Black;
            }
            catch { }
        }

        static TextBox CreateTdiValue() => new()
        {
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill,
            Font = DashboardTypography.Create(FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(2, 1, 2, 1),
            ReadOnly = true,
            Text = "N/A",
            TextAlign = HorizontalAlignment.Center
        };

        void ConfigureTdiRow()
        {
            _tdiData.Name = "tlpTdiData";
            _tdiData.BackColor = Color.Black;
            _tdiData.ColumnCount = 5;
            _tdiData.RowCount = 2;
            _tdiData.Dock = DockStyle.Top;
            _tdiData.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            _tdiData.Height = 54;
            _tdiData.Margin = new Padding(0);
            for (var index = 0; index < 5; index++)
                _tdiData.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            _tdiData.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _tdiData.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            var labels = new[]
            {
                CreateTdiLabel("TDI Direction"),
                CreateTdiLabel("TDI Strength"),
                CreateTdiLabel("Market State"),
                CreateTdiLabel("Cross"),
                CreateTdiLabel("Divergence")
            };
            var values = new[]
            {
                _txtTdiDirection,
                _txtTdiStrength,
                _txtTdiMarketState,
                _txtTdiCross,
                _txtTdiDivergence
            };
            var valueNames = new[]
            {
                "txtTdiDirection",
                "txtTdiStrength",
                "txtTdiMarketState",
                "txtTdiCross",
                "txtTdiDivergence"
            };
            for (var column = 0; column < labels.Length; column++)
            {
                values[column].Name = valueNames[column];
                values[column].AccessibleName = labels[column].Text;
                values[column].AccessibleDescription = "Warming";
                _tdiData.Controls.Add(labels[column], column, 0);
                _tdiData.Controls.Add(values[column], column, 1);
            }
            Controls.Add(_tdiData);
            Controls.SetChildIndex(_tdiData, 1);
        }

        static Label CreateTdiLabel(string text) => new()
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Font = DashboardTypography.Create(FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(2, 0, 2, 0),
            Text = text,
            TextAlign = ContentAlignment.BottomCenter
        };

        void ConfigureTypographyAndAlignment()
        {
            foreach (var control in Descendants(this))
            {
                if (control is Label label)
                {
                    label.AutoSize = false;
                    label.Dock = DockStyle.Fill;
                    label.Font = DashboardTypography.Create(FontStyle.Bold);
                    label.Margin = new Padding(2, 0, 2, 0);
                    label.Text = label.Text.Trim();
                    label.TextAlign = ContentAlignment.BottomCenter;
                }
                else if (control is TextBox value)
                {
                    value.Dock = DockStyle.Fill;
                    value.Font = DashboardTypography.Create(FontStyle.Bold);
                    value.Margin = new Padding(2, 1, 2, 1);
                    value.TextAlign = HorizontalAlignment.Center;
                }
            }
        }

        static IEnumerable<Control> Descendants(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
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
            _txtTdiDirection.Text = e.TdiDirection;
            _txtTdiStrength.Text = e.TdiStrength;
            _txtTdiMarketState.Text = e.TdiMarketState;
            _txtTdiCross.Text = e.TdiCross;
            _txtTdiDivergence.Text = e.TdiDivergence;
            _txtTdiDirection.AccessibleDescription = e.TdiDirection;
            _txtTdiStrength.AccessibleDescription = e.TdiStrength;
            _txtTdiMarketState.AccessibleDescription = e.TdiMarketState;
            _txtTdiCross.AccessibleDescription = e.TdiCross;
            _txtTdiDivergence.AccessibleDescription = e.TdiDivergence;
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
            const int rowBreathingRoom = 1;
            var dataTables = new[]
            {
                tlpMarketOutlook,
                tlpMarketData,
                _tdiData,
                tlpMarketTrendData
            };
            var labelRowHeight = dataTables
                .SelectMany(table => table.Controls.OfType<Label>())
                .Where(label => label != _snapshotStatus)
                .Max(label => label.PreferredSize.Height + label.Margin.Vertical) + rowBreathingRoom;
            var valueRowHeight = dataTables
                .SelectMany(table => table.Controls.OfType<TextBox>())
                .Max(value => value.PreferredSize.Height + value.Margin.Vertical) + rowBreathingRoom;

            SetPairRowHeights(tlpMarketOutlook, 1, labelRowHeight, valueRowHeight);
            SetPairRowHeights(tlpMarketData, 3, labelRowHeight, valueRowHeight);
            SetPairRowHeights(_tdiData, 1, labelRowHeight, valueRowHeight);
            SetPairRowHeights(tlpMarketTrendData, 1, labelRowHeight, valueRowHeight);

            var statusRowHeight = Math.Max(18, _snapshotStatus.PreferredSize.Height + 2);
            tlpMarketOutlook.RowStyles[2].SizeType = SizeType.Absolute;
            tlpMarketOutlook.RowStyles[2].Height = statusRowHeight;
            tlpMarketOutlook.Height = labelRowHeight + valueRowHeight + statusRowHeight;
            tlpMarketData.Height = 3 * (labelRowHeight + valueRowHeight);
            _tdiData.Height = labelRowHeight + valueRowHeight;
            tlpMarketTrendData.Height = labelRowHeight + valueRowHeight;
            parentControl.Height = tlpMarketOutlook.Height + tlpMarketData.Height
                + _tdiData.Height + tlpMarketTrendData.Height + CompactBottomClearance;
        }

        static void SetPairRowHeights(
            TableLayoutPanel table,
            int pairCount,
            int labelRowHeight,
            int valueRowHeight)
        {
            for (var pair = 0; pair < pairCount; pair++)
            {
                var labelRow = pair * 2;
                var valueRow = labelRow + 1;
                table.RowStyles[labelRow].SizeType = SizeType.Absolute;
                table.RowStyles[labelRow].Height = labelRowHeight;
                table.RowStyles[valueRow].SizeType = SizeType.Absolute;
                table.RowStyles[valueRow].Height = valueRowHeight;
            }
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
