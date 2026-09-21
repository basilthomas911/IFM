namespace TomasAI.IFM.UI.Net.Views.App
{
    partial class MarketDataView
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            var esArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            var esLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
            var esPrice = new System.Windows.Forms.DataVisualization.Charting.Series();
            var esUpperTrigger = new System.Windows.Forms.DataVisualization.Charting.Series();
            var esLowerTrigger = new System.Windows.Forms.DataVisualization.Charting.Series();
            var bbArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            var bbLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
            var bbClose = new System.Windows.Forms.DataVisualization.Charting.Series();
            var bbEma20 = new System.Windows.Forms.DataVisualization.Charting.Series();
            var bbUpper = new System.Windows.Forms.DataVisualization.Charting.Series();
            var bbLower = new System.Windows.Forms.DataVisualization.Charting.Series();
            var vxArea = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            var vxLegend = new System.Windows.Forms.DataVisualization.Charting.Legend();
            var vxPrice = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.tabMarketData = new DarkTabControl();
            this.tabPageEs = new System.Windows.Forms.TabPage();
            this.graphES = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.tabPageEsBb = new System.Windows.Forms.TabPage();
            this.graphEsBollinger = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.tabPageVx = new System.Windows.Forms.TabPage();
            this.graphVIX = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.vixBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.esBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.tabMarketData.SuspendLayout();
            this.tabPageEs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.graphES)).BeginInit();
            this.tabPageEsBb.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.graphEsBollinger)).BeginInit();
            this.tabPageVx.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.graphVIX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.vixBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.esBindingSource)).BeginInit();
            this.SuspendLayout();

            this.tabMarketData.BackColor = System.Drawing.Color.Black;
            this.tabMarketData.Controls.Add(this.tabPageEs);
            this.tabMarketData.Controls.Add(this.tabPageEsBb);
            this.tabMarketData.Controls.Add(this.tabPageVx);
            this.tabMarketData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMarketData.ForeColor = System.Drawing.Color.White;
            this.tabMarketData.Location = new System.Drawing.Point(0, 0);
            this.tabMarketData.Name = "tabMarketData";
            this.tabMarketData.SelectedIndex = 0;
            this.tabMarketData.Size = new System.Drawing.Size(360, 300);
            this.tabMarketData.TabIndex = 1;

            this.tabPageEs.BackColor = System.Drawing.Color.Black;
            this.tabPageEs.Controls.Add(this.graphES);
            this.tabPageEs.Location = new System.Drawing.Point(4, 22);
            this.tabPageEs.Name = "tabPageEs";
            this.tabPageEs.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageEs.Size = new System.Drawing.Size(352, 274);
            this.tabPageEs.TabIndex = 0;
            this.tabPageEs.Text = "ES";
            this.tabPageEs.UseVisualStyleBackColor = false;

            this.graphES.BackColor = System.Drawing.Color.Black;
            esArea.AxisX.IsMarginVisible = false;
            esArea.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            esArea.AxisX2.IsMarginVisible = false;
            esArea.AxisY.IsMarginVisible = false;
            esArea.AxisY.LabelStyle.ForeColor = System.Drawing.Color.White;
            esArea.AxisY2.IsMarginVisible = false;
            esArea.AxisY2.LabelStyle.ForeColor = System.Drawing.Color.White;
            esArea.BackColor = System.Drawing.Color.Black;
            esArea.IsSameFontSizeForAllAxes = true;
            esArea.Name = "ChartArea1";
            this.graphES.ChartAreas.Add(esArea);
            this.graphES.Dock = System.Windows.Forms.DockStyle.Fill;
            esLegend.Enabled = false;
            esLegend.Name = "Legend1";
            this.graphES.Legends.Add(esLegend);
            this.graphES.Location = new System.Drawing.Point(3, 3);
            this.graphES.Name = "graphES";
            ConfigureLine(esPrice, "ES Close", System.Drawing.Color.Yellow, false);
            ConfigureLine(esUpperTrigger, "Upper Trigger", System.Drawing.Color.Lime, false);
            ConfigureLine(esLowerTrigger, "Lower Trigger", System.Drawing.Color.Red, false);
            esPrice.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            esUpperTrigger.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            esLowerTrigger.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            this.graphES.Series.Add(esPrice);
            this.graphES.Series.Add(esUpperTrigger);
            this.graphES.Series.Add(esLowerTrigger);
            this.graphES.Size = new System.Drawing.Size(346, 268);
            this.graphES.TabIndex = 0;

            this.tabPageEsBb.BackColor = System.Drawing.Color.Black;
            this.tabPageEsBb.Controls.Add(this.graphEsBollinger);
            this.tabPageEsBb.Location = new System.Drawing.Point(4, 22);
            this.tabPageEsBb.Name = "tabPageEsBb";
            this.tabPageEsBb.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageEsBb.Size = new System.Drawing.Size(352, 274);
            this.tabPageEsBb.TabIndex = 1;
            this.tabPageEsBb.Text = "ES-BB";
            this.tabPageEsBb.UseVisualStyleBackColor = false;

            this.graphEsBollinger.BackColor = System.Drawing.Color.Black;
            bbArea.AxisX.IsMarginVisible = false;
            bbArea.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            bbArea.AxisY2.IsMarginVisible = false;
            bbArea.AxisY2.LabelStyle.ForeColor = System.Drawing.Color.White;
            bbArea.BackColor = System.Drawing.Color.Black;
            bbArea.IsSameFontSizeForAllAxes = true;
            bbArea.Name = "ChartArea1";
            this.graphEsBollinger.ChartAreas.Add(bbArea);
            this.graphEsBollinger.Dock = System.Windows.Forms.DockStyle.Fill;
            bbLegend.BackColor = System.Drawing.Color.Black;
            bbLegend.ForeColor = System.Drawing.Color.White;
            bbLegend.Enabled = false;
            bbLegend.Name = "Legend1";
            this.graphEsBollinger.Legends.Add(bbLegend);
            this.graphEsBollinger.Location = new System.Drawing.Point(3, 3);
            this.graphEsBollinger.Name = "graphEsBollinger";
            ConfigureLine(bbClose, "ES Close", System.Drawing.Color.Yellow, false);
            ConfigureLine(bbEma20, "20 EMA", System.Drawing.Color.Blue, false);
            ConfigureLine(bbUpper, "Upper Band", System.Drawing.Color.Green, false);
            ConfigureLine(bbLower, "Lower Band", System.Drawing.Color.Red, false);
            this.graphEsBollinger.Series.Add(bbClose);
            this.graphEsBollinger.Series.Add(bbEma20);
            this.graphEsBollinger.Series.Add(bbUpper);
            this.graphEsBollinger.Series.Add(bbLower);
            this.graphEsBollinger.Size = new System.Drawing.Size(346, 268);
            this.graphEsBollinger.TabIndex = 0;

            this.tabPageVx.BackColor = System.Drawing.Color.Black;
            this.tabPageVx.Controls.Add(this.graphVIX);
            this.tabPageVx.Location = new System.Drawing.Point(4, 22);
            this.tabPageVx.Name = "tabPageVx";
            this.tabPageVx.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageVx.Size = new System.Drawing.Size(352, 274);
            this.tabPageVx.TabIndex = 2;
            this.tabPageVx.Text = "VX";
            this.tabPageVx.UseVisualStyleBackColor = false;

            this.graphVIX.BackColor = System.Drawing.Color.Black;
            vxArea.AxisX.Enabled = System.Windows.Forms.DataVisualization.Charting.AxisEnabled.True;
            vxArea.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            vxArea.AxisY2.LabelStyle.ForeColor = System.Drawing.Color.White;
            vxArea.BackColor = System.Drawing.Color.Black;
            vxArea.Name = "ChartArea1";
            this.graphVIX.ChartAreas.Add(vxArea);
            this.graphVIX.Dock = System.Windows.Forms.DockStyle.Fill;
            vxLegend.BackColor = System.Drawing.Color.Black;
            vxLegend.Enabled = false;
            vxLegend.Name = "Legend1";
            this.graphVIX.Legends.Add(vxLegend);
            this.graphVIX.Location = new System.Drawing.Point(3, 3);
            this.graphVIX.Name = "graphVIX";
            ConfigureLine(vxPrice, "VX", System.Drawing.Color.Fuchsia, false);
            vxPrice.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.DateTime;
            this.graphVIX.Series.Add(vxPrice);
            this.graphVIX.Size = new System.Drawing.Size(346, 268);
            this.graphVIX.TabIndex = 1;

            this.esBindingSource.AllowNew = true;
            this.esBindingSource.DataSource = typeof(TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.FuturesBarDataReadModel);

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.Controls.Add(this.tabMarketData);
            this.Name = "MarketDataView";
            this.Size = new System.Drawing.Size(360, 300);
            this.tabMarketData.ResumeLayout(false);
            this.tabPageEs.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.graphES)).EndInit();
            this.tabPageEsBb.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.graphEsBollinger)).EndInit();
            this.tabPageVx.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.graphVIX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.vixBindingSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.esBindingSource)).EndInit();
            this.ResumeLayout(false);
        }

        static void ConfigureLine(
            System.Windows.Forms.DataVisualization.Charting.Series series,
            string name,
            System.Drawing.Color color,
            bool visibleInLegend)
        {
            series.BorderWidth = 2;
            series.ChartArea = "ChartArea1";
            series.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series.Color = color;
            series.IsVisibleInLegend = visibleInLegend;
            series.Legend = "Legend1";
            series.Name = name;
            series.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;
            series.YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            series.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
        }

        private System.Windows.Forms.TabControl tabMarketData;
        private System.Windows.Forms.TabPage tabPageEs;
        private System.Windows.Forms.TabPage tabPageEsBb;
        private System.Windows.Forms.TabPage tabPageVx;
        private System.Windows.Forms.DataVisualization.Charting.Chart graphES;
        private System.Windows.Forms.DataVisualization.Charting.Chart graphEsBollinger;
        private System.Windows.Forms.DataVisualization.Charting.Chart graphVIX;
        private System.Windows.Forms.BindingSource esBindingSource;
        private System.Windows.Forms.BindingSource vixBindingSource;
    }
}
