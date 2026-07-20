namespace TomasAI.IFM.Views.App
{
    partial class MarketDataView
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series2 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.Series series3 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea2 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend2 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series4 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.tabMarketData = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.graphES = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.graphVIX = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.vixBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.esBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.tabMarketData.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.graphES)).BeginInit();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.graphVIX)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.vixBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.esBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // tabMarketData
            // 
            this.tabMarketData.Controls.Add(this.tabPage1);
            this.tabMarketData.Controls.Add(this.tabPage2);
            this.tabMarketData.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMarketData.Location = new System.Drawing.Point(0, 0);
            this.tabMarketData.Name = "tabMarketData";
            this.tabMarketData.SelectedIndex = 0;
            this.tabMarketData.Size = new System.Drawing.Size(360, 300);
            this.tabMarketData.TabIndex = 1;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.Color.Black;
            this.tabPage1.Controls.Add(this.graphES);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(352, 274);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "ES";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // graphES
            // 
            this.graphES.BackColor = System.Drawing.Color.Black;
            chartArea1.AxisX.IsMarginVisible = false;
            chartArea1.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea1.AxisX2.IsMarginVisible = false;
            chartArea1.AxisY.IsMarginVisible = false;
            chartArea1.AxisY.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea1.AxisY2.IsMarginVisible = false;
            chartArea1.AxisY2.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea1.BackColor = System.Drawing.Color.Black;
            chartArea1.IsSameFontSizeForAllAxes = true;
            chartArea1.Name = "ChartArea1";
            this.graphES.ChartAreas.Add(chartArea1);
            this.graphES.Dock = System.Windows.Forms.DockStyle.Fill;
            legend1.Enabled = false;
            legend1.Name = "Legend1";
            this.graphES.Legends.Add(legend1);
            this.graphES.Location = new System.Drawing.Point(3, 3);
            this.graphES.Name = "graphES";
            series1.BorderWidth = 2;
            series1.ChartArea = "ChartArea1";
            series1.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series1.Color = System.Drawing.Color.Yellow;
            series1.IsVisibleInLegend = false;
            series1.Legend = "Legend1";
            series1.Name = "Series1";
            series1.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            series1.YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            series1.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            series2.ChartArea = "ChartArea1";
            series2.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series2.Color = System.Drawing.Color.Lime;
            series2.IsVisibleInLegend = false;
            series2.Legend = "Legend1";
            series2.Name = "Series2";
            series2.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            series2.YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            series2.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            series3.ChartArea = "ChartArea1";
            series3.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series3.Color = System.Drawing.Color.Red;
            series3.IsVisibleInLegend = false;
            series3.Legend = "Legend1";
            series3.Name = "Series3";
            series3.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            series3.YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            series3.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.graphES.Series.Add(series1);
            this.graphES.Series.Add(series2);
            this.graphES.Series.Add(series3);
            this.graphES.Size = new System.Drawing.Size(346, 268);
            this.graphES.TabIndex = 0;
            this.graphES.Text = "chart1";
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.Color.Black;
            this.tabPage2.Controls.Add(this.graphVIX);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(352, 274);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "VIX";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // graphVIX
            // 
            this.graphVIX.BackColor = System.Drawing.Color.Black;
            this.graphVIX.BorderlineColor = System.Drawing.Color.Black;
            this.graphVIX.BorderlineWidth = 0;
            this.graphVIX.BorderSkin.BackColor = System.Drawing.Color.Black;
            this.graphVIX.BorderSkin.BorderWidth = 0;
            this.graphVIX.BorderSkin.PageColor = System.Drawing.Color.Black;
            chartArea2.AxisX.Enabled = System.Windows.Forms.DataVisualization.Charting.AxisEnabled.True;
            chartArea2.AxisX.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea2.AxisY2.LabelStyle.ForeColor = System.Drawing.Color.White;
            chartArea2.BackColor = System.Drawing.Color.Black;
            chartArea2.BorderWidth = 0;
            chartArea2.Name = "ChartArea1";
            this.graphVIX.ChartAreas.Add(chartArea2);
            this.graphVIX.Dock = System.Windows.Forms.DockStyle.Fill;
            legend2.BackColor = System.Drawing.Color.Black;
            legend2.Enabled = false;
            legend2.Name = "Legend1";
            this.graphVIX.Legends.Add(legend2);
            this.graphVIX.Location = new System.Drawing.Point(3, 3);
            this.graphVIX.Margin = new System.Windows.Forms.Padding(0);
            this.graphVIX.Name = "graphVIX";
            series4.BorderWidth = 2;
            series4.ChartArea = "ChartArea1";
            series4.ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            series4.Color = System.Drawing.Color.Fuchsia;
            series4.Legend = "Legend1";
            series4.Name = "Series1";
            series4.XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            series4.YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            series4.YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            this.graphVIX.Series.Add(series4);
            this.graphVIX.Size = new System.Drawing.Size(346, 268);
            this.graphVIX.TabIndex = 1;
            // 
            // esBindingSource
            // 
            this.esBindingSource.AllowNew = true;
            this.esBindingSource.DataSource = typeof(TomasAI.IFM.Shared.MarketDataFeed.ViewModels.FuturesBarDataReadModel);
            // 
            // MarketDataView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.Controls.Add(this.tabMarketData);
            this.Name = "MarketDataView";
            this.Size = new System.Drawing.Size(360, 300);
            this.tabMarketData.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.graphES)).EndInit();
            this.tabPage2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.graphVIX)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.vixBindingSource)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.esBindingSource)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabMarketData;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataVisualization.Charting.Chart graphVIX;
        private System.Windows.Forms.BindingSource esBindingSource;
        private System.Windows.Forms.BindingSource vixBindingSource;
        private System.Windows.Forms.DataVisualization.Charting.Chart graphES;
    }
}
