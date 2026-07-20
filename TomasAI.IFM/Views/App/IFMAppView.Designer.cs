namespace TomasAI.IFM.Views.App
{
    partial class IFMAppView
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IFMAppView));
            this.statusBar = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.tradeSplitter = new System.Windows.Forms.SplitContainer();
            this.tabTradeBlotter = new System.Windows.Forms.TabControl();
            this.pnlAppView = new System.Windows.Forms.Panel();
            this.pnlStatusConsole = new System.Windows.Forms.Panel();
            this.pnlEconomicCalendar = new System.Windows.Forms.Panel();
            this.pnlMarketData = new System.Windows.Forms.Panel();
            this.pnlMarketOutlook = new System.Windows.Forms.Panel();
            this.tradeButton = new System.Windows.Forms.ToolStripButton();
            this.marketDataButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.btnCloseOrder = new System.Windows.Forms.ToolStripButton();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.fundButton = new System.Windows.Forms.ToolStripButton();
            this.referenceButton = new System.Windows.Forms.ToolStripButton();
            this.systemAdminButton = new System.Windows.Forms.ToolStripButton();
            this.statusConsoleLogViewModelBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.statusConsoleView1 = new TomasAI.IFM.Views.App.StatusConsoleView();
            this.economicCalendarView1 = new TomasAI.IFM.Views.App.MarketEconomicCalendarView();
            this.marketDataView1 = new TomasAI.IFM.Views.App.MarketDataView();
            this.marketOutlookView1 = new TomasAI.IFM.Views.App.MarketOutlookView();
            this.statusBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tradeSplitter)).BeginInit();
            this.tradeSplitter.Panel1.SuspendLayout();
            this.tradeSplitter.Panel2.SuspendLayout();
            this.tradeSplitter.SuspendLayout();
            this.pnlAppView.SuspendLayout();
            this.pnlStatusConsole.SuspendLayout();
            this.pnlEconomicCalendar.SuspendLayout();
            this.pnlMarketData.SuspendLayout();
            this.pnlMarketOutlook.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statusConsoleLogViewModelBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // statusBar
            // 
            this.statusBar.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusBar.Location = new System.Drawing.Point(0, 848);
            this.statusBar.Name = "statusBar";
            this.statusBar.Padding = new System.Windows.Forms.Padding(1, 0, 10, 0);
            this.statusBar.Size = new System.Drawing.Size(2637, 24);
            this.statusBar.TabIndex = 1;
            this.statusBar.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatus.ForeColor = System.Drawing.SystemColors.ControlLightLight;
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(46, 19);
            this.lblStatus.Text = "Ready";
            // 
            // tradeSplitter
            // 
            this.tradeSplitter.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.tradeSplitter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tradeSplitter.Location = new System.Drawing.Point(0, 27);
            this.tradeSplitter.Margin = new System.Windows.Forms.Padding(2);
            this.tradeSplitter.Name = "tradeSplitter";
            // 
            // tradeSplitter.Panel1
            // 
            this.tradeSplitter.Panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.tradeSplitter.Panel1.Controls.Add(this.tabTradeBlotter);
            // 
            // tradeSplitter.Panel2
            // 
            this.tradeSplitter.Panel2.Controls.Add(this.pnlAppView);
            this.tradeSplitter.Size = new System.Drawing.Size(2637, 821);
            this.tradeSplitter.SplitterDistance = 2161;
            this.tradeSplitter.SplitterIncrement = 2;
            this.tradeSplitter.SplitterWidth = 5;
            this.tradeSplitter.TabIndex = 4;
            this.tradeSplitter.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.tradeSplitter_SplitterMoved);
            // 
            // tabTradeBlotter
            // 
            this.tabTradeBlotter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabTradeBlotter.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabTradeBlotter.Location = new System.Drawing.Point(0, 0);
            this.tabTradeBlotter.Margin = new System.Windows.Forms.Padding(0);
            this.tabTradeBlotter.Name = "tabTradeBlotter";
            this.tabTradeBlotter.SelectedIndex = 0;
            this.tabTradeBlotter.Size = new System.Drawing.Size(2161, 821);
            this.tabTradeBlotter.TabIndex = 4;
            this.tabTradeBlotter.SelectedIndexChanged += new System.EventHandler(this.tabTradeBlotter_SelectedIndexChanged);
            // 
            // pnlAppView
            // 
            this.pnlAppView.AutoSize = true;
            this.pnlAppView.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.pnlAppView.Controls.Add(this.pnlStatusConsole);
            this.pnlAppView.Controls.Add(this.pnlEconomicCalendar);
            this.pnlAppView.Controls.Add(this.pnlMarketData);
            this.pnlAppView.Controls.Add(this.pnlMarketOutlook);
            this.pnlAppView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlAppView.Location = new System.Drawing.Point(0, 0);
            this.pnlAppView.Margin = new System.Windows.Forms.Padding(2);
            this.pnlAppView.Name = "pnlAppView";
            this.pnlAppView.Size = new System.Drawing.Size(471, 821);
            this.pnlAppView.TabIndex = 2;
            // 
            // pnlStatusConsole
            // 
            this.pnlStatusConsole.Controls.Add(this.statusConsoleView1);
            this.pnlStatusConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlStatusConsole.Location = new System.Drawing.Point(0, 767);
            this.pnlStatusConsole.Name = "pnlStatusConsole";
            this.pnlStatusConsole.Size = new System.Drawing.Size(471, 54);
            this.pnlStatusConsole.TabIndex = 1;
            // 
            // pnlEconomicCalendar
            // 
            this.pnlEconomicCalendar.Controls.Add(this.economicCalendarView1);
            this.pnlEconomicCalendar.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlEconomicCalendar.Location = new System.Drawing.Point(0, 496);
            this.pnlEconomicCalendar.Name = "pnlEconomicCalendar";
            this.pnlEconomicCalendar.Size = new System.Drawing.Size(471, 271);
            this.pnlEconomicCalendar.TabIndex = 5;
            // 
            // pnlMarketData
            // 
            this.pnlMarketData.Controls.Add(this.marketDataView1);
            this.pnlMarketData.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMarketData.Location = new System.Drawing.Point(0, 300);
            this.pnlMarketData.Name = "pnlMarketData";
            this.pnlMarketData.Size = new System.Drawing.Size(471, 196);
            this.pnlMarketData.TabIndex = 3;
            // 
            // pnlMarketOutlook
            // 
            this.pnlMarketOutlook.Controls.Add(this.marketOutlookView1);
            this.pnlMarketOutlook.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlMarketOutlook.Location = new System.Drawing.Point(0, 0);
            this.pnlMarketOutlook.Name = "pnlMarketOutlook";
            this.pnlMarketOutlook.Size = new System.Drawing.Size(471, 300);
            this.pnlMarketOutlook.TabIndex = 1;
            // 
            // tradeButton
            // 
            this.tradeButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.tradeButton.Enabled = false;
            this.tradeButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tradeButton.Image = ((System.Drawing.Image)(resources.GetObject("tradeButton.Image")));
            this.tradeButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tradeButton.Name = "tradeButton";
            this.tradeButton.Size = new System.Drawing.Size(50, 24);
            this.tradeButton.Text = "Trade";
            this.tradeButton.Click += new System.EventHandler(this.tradeButton_Click);
            // 
            // marketDataButton
            // 
            this.marketDataButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.marketDataButton.Enabled = false;
            this.marketDataButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.marketDataButton.Image = ((System.Drawing.Image)(resources.GetObject("marketDataButton.Image")));
            this.marketDataButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.marketDataButton.Name = "marketDataButton";
            this.marketDataButton.Size = new System.Drawing.Size(95, 24);
            this.marketDataButton.Text = "Market Data";
            this.marketDataButton.Click += new System.EventHandler(this.marketDataButton_Click);
            // 
            // toolStripSeparator
            // 
            this.toolStripSeparator.Name = "toolStripSeparator";
            this.toolStripSeparator.Size = new System.Drawing.Size(6, 27);
            // 
            // btnCloseOrder
            // 
            this.btnCloseOrder.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.btnCloseOrder.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnCloseOrder.Font = new System.Drawing.Font("Segoe UI", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCloseOrder.Image = ((System.Drawing.Image)(resources.GetObject("btnCloseOrder.Image")));
            this.btnCloseOrder.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnCloseOrder.Name = "btnCloseOrder";
            this.btnCloseOrder.Size = new System.Drawing.Size(89, 24);
            this.btnCloseOrder.Text = "Close Order:";
            this.btnCloseOrder.Visible = false;
            this.btnCloseOrder.Click += new System.EventHandler(this.btnCloseOrder_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tradeButton,
            this.marketDataButton,
            this.fundButton,
            this.referenceButton,
            this.systemAdminButton,
            this.toolStripSeparator,
            this.btnCloseOrder});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(2637, 27);
            this.toolStrip1.TabIndex = 0;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // fundButton
            // 
            this.fundButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.fundButton.Enabled = false;
            this.fundButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.fundButton.Image = ((System.Drawing.Image)(resources.GetObject("fundButton.Image")));
            this.fundButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.fundButton.Name = "fundButton";
            this.fundButton.Size = new System.Drawing.Size(45, 24);
            this.fundButton.Text = "Fund";
            this.fundButton.Click += new System.EventHandler(this.fundButton_Click);
            // 
            // referenceButton
            // 
            this.referenceButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.referenceButton.Enabled = false;
            this.referenceButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.referenceButton.Image = ((System.Drawing.Image)(resources.GetObject("referenceButton.Image")));
            this.referenceButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.referenceButton.Name = "referenceButton";
            this.referenceButton.Size = new System.Drawing.Size(79, 24);
            this.referenceButton.Text = "Reference";
            this.referenceButton.Click += new System.EventHandler(this.referenceButton_Click);
            // 
            // systemAdminButton
            // 
            this.systemAdminButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.systemAdminButton.Enabled = false;
            this.systemAdminButton.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.systemAdminButton.Image = ((System.Drawing.Image)(resources.GetObject("systemAdminButton.Image")));
            this.systemAdminButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.systemAdminButton.Name = "systemAdminButton";
            this.systemAdminButton.Size = new System.Drawing.Size(108, 24);
            this.systemAdminButton.Text = "System Admin";
            this.systemAdminButton.Click += new System.EventHandler(this.systemAdminButton_Click);
            // 
            // statusConsoleLogViewModelBindingSource
            // 
            this.statusConsoleLogViewModelBindingSource.DataSource = typeof(TomasAI.IFM.Shared.Log.ViewModels.StatusConsoleLogReadModel);
            this.statusConsoleLogViewModelBindingSource.CurrentChanged += new System.EventHandler(this.statusConsoleLogViewModelBindingSource_CurrentChanged);
            // 
            // statusConsoleView1
            // 
            this.statusConsoleView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statusConsoleView1.Location = new System.Drawing.Point(0, 0);
            this.statusConsoleView1.Margin = new System.Windows.Forms.Padding(4);
            this.statusConsoleView1.Name = "statusConsoleView1";
            this.statusConsoleView1.Size = new System.Drawing.Size(471, 271);
            this.statusConsoleView1.TabIndex = 0;
            // 
            // economicCalendarView1
            // 
            this.economicCalendarView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.economicCalendarView1.Location = new System.Drawing.Point(0, 0);
            this.economicCalendarView1.Margin = new System.Windows.Forms.Padding(4);
            this.economicCalendarView1.Name = "economicCalendarView1";
            this.economicCalendarView1.Size = new System.Drawing.Size(471, 271);
            this.economicCalendarView1.TabIndex = 0;
            this.economicCalendarView1.Load += new System.EventHandler(this.economicCalendarView1_Load);
            // 
            // marketDataView1
            // 
            this.marketDataView1.BackColor = System.Drawing.Color.Black;
            this.marketDataView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.marketDataView1.Location = new System.Drawing.Point(0, 0);
            this.marketDataView1.Margin = new System.Windows.Forms.Padding(4);
            this.marketDataView1.Name = "marketDataView1";
            this.marketDataView1.Size = new System.Drawing.Size(471, 196);
            this.marketDataView1.TabIndex = 0;
            this.marketDataView1.Load += new System.EventHandler(this.marketDataView1_Load);
            // 
            // marketOutlookView1
            // 
            this.marketOutlookView1.BackColor = System.Drawing.Color.Black;
            this.marketOutlookView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.marketOutlookView1.Location = new System.Drawing.Point(0, 0);
            this.marketOutlookView1.Margin = new System.Windows.Forms.Padding(4);
            this.marketOutlookView1.Name = "marketOutlookView1";
            this.marketOutlookView1.Size = new System.Drawing.Size(471, 300);
            this.marketOutlookView1.TabIndex = 0;
            // 
            // IFMAppView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.ClientSize = new System.Drawing.Size(2637, 872);
            this.Controls.Add(this.tradeSplitter);
            this.Controls.Add(this.statusBar);
            this.Controls.Add(this.toolStrip1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "IFMAppView";
            this.Text = "Investment Fund Manager";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.IFMApp_FormClosing);
            this.Load += new System.EventHandler(this.IFMApp_Load);
            this.Resize += new System.EventHandler(this.IFMApp_Resize);
            this.statusBar.ResumeLayout(false);
            this.statusBar.PerformLayout();
            this.tradeSplitter.Panel1.ResumeLayout(false);
            this.tradeSplitter.Panel2.ResumeLayout(false);
            this.tradeSplitter.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tradeSplitter)).EndInit();
            this.tradeSplitter.ResumeLayout(false);
            this.pnlAppView.ResumeLayout(false);
            this.pnlEconomicCalendar.ResumeLayout(false);
            this.pnlMarketData.ResumeLayout(false);
            this.pnlMarketOutlook.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statusConsoleLogViewModelBindingSource)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.StatusStrip statusBar;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.SplitContainer tradeSplitter;
        private System.Windows.Forms.TabControl tabTradeBlotter;
        private System.Windows.Forms.BindingSource statusConsoleLogViewModelBindingSource;
        private System.Windows.Forms.Panel pnlAppView;
        private System.Windows.Forms.ToolStripButton tradeButton;
        private System.Windows.Forms.ToolStripButton marketDataButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator;
        private System.Windows.Forms.ToolStripButton btnCloseOrder;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton fundButton;
        private System.Windows.Forms.ToolStripButton systemAdminButton;
        private System.Windows.Forms.ToolStripButton referenceButton;
        private System.Windows.Forms.Panel pnlMarketOutlook;
        private MarketOutlookView marketOutlookView1;
        private System.Windows.Forms.Panel pnlMarketData;
        private MarketDataView marketDataView1;
        private System.Windows.Forms.Panel pnlEconomicCalendar;
        private MarketEconomicCalendarView economicCalendarView1;
        private System.Windows.Forms.Panel pnlStatusConsole;
        private StatusConsoleView statusConsoleView1;
    }
}