namespace TomasAI.IFM.UI.Net.Views.App
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IFMAppView));
            statusBar = new StatusStrip();
            lblStatus = new ToolStripStatusLabel();
            tradeSplitter = new SplitContainer();
            tabTradeBlotter = new TabControl();
            pnlAppView = new Panel();
            pnlStatusConsole = new Panel();
            statusConsoleView1 = new StatusConsoleView();
            pnlEconomicCalendar = new Panel();
            economicCalendarView1 = new MarketEconomicCalendarView();
            pnlMarketData = new Panel();
            marketDataView1 = new MarketDataView();
            pnlMarketOutlook = new Panel();
            marketOutlookView1 = new MarketOutlookView();
            tradeButton = new ToolStripButton();
            marketDataButton = new ToolStripButton();
            toolStripSeparator = new ToolStripSeparator();
            btnCloseOrder = new ToolStripButton();
            toolStrip1 = new ToolStrip();
            fundButton = new ToolStripButton();
            referenceButton = new ToolStripButton();
            systemAdminButton = new ToolStripButton();
            statusConsoleLogViewModelBindingSource = new BindingSource(components);
            statusBar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)tradeSplitter).BeginInit();
            tradeSplitter.Panel1.SuspendLayout();
            tradeSplitter.Panel2.SuspendLayout();
            tradeSplitter.SuspendLayout();
            pnlAppView.SuspendLayout();
            pnlStatusConsole.SuspendLayout();
            pnlEconomicCalendar.SuspendLayout();
            pnlMarketData.SuspendLayout();
            pnlMarketOutlook.SuspendLayout();
            toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)statusConsoleLogViewModelBindingSource).BeginInit();
            SuspendLayout();
            // 
            // statusBar
            // 
            statusBar.BackColor = Color.Black;
            statusBar.ImageScalingSize = new Size(24, 24);
            statusBar.Items.AddRange(new ToolStripItem[] { lblStatus });
            statusBar.Location = new Point(0, 848);
            statusBar.Name = "statusBar";
            statusBar.Padding = new Padding(1, 0, 10, 0);
            statusBar.Size = new Size(2637, 24);
            statusBar.TabIndex = 1;
            statusBar.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            lblStatus.BackColor = Color.Black;
            lblStatus.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblStatus.ForeColor = Color.White;
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(46, 19);
            lblStatus.Text = "Ready";
            // 
            // tradeSplitter
            // 
            tradeSplitter.BackColor = SystemColors.ControlDarkDark;
            tradeSplitter.Dock = DockStyle.Fill;
            tradeSplitter.Location = new Point(0, 27);
            tradeSplitter.Margin = new Padding(2);
            tradeSplitter.Name = "tradeSplitter";
            // 
            // tradeSplitter.Panel1
            // 
            tradeSplitter.Panel1.BackColor = Color.FromArgb(64, 64, 64);
            tradeSplitter.Panel1.Controls.Add(tabTradeBlotter);
            // 
            // tradeSplitter.Panel2
            // 
            tradeSplitter.Panel2.Controls.Add(pnlAppView);
            tradeSplitter.Size = new Size(2637, 821);
            tradeSplitter.SplitterDistance = 2161;
            tradeSplitter.SplitterIncrement = 2;
            tradeSplitter.SplitterWidth = 5;
            tradeSplitter.TabIndex = 4;
            tradeSplitter.SplitterMoved += tradeSplitter_SplitterMoved;
            // 
            // tabTradeBlotter
            // 
            tabTradeBlotter.Dock = DockStyle.Fill;
            tabTradeBlotter.Font = new Font("Microsoft Sans Serif", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            tabTradeBlotter.Location = new Point(0, 0);
            tabTradeBlotter.Margin = new Padding(0);
            tabTradeBlotter.Name = "tabTradeBlotter";
            tabTradeBlotter.SelectedIndex = 0;
            tabTradeBlotter.Size = new Size(2161, 821);
            tabTradeBlotter.TabIndex = 4;
            tabTradeBlotter.SelectedIndexChanged += tabTradeBlotter_SelectedIndexChanged;
            // 
            // pnlAppView
            // 
            pnlAppView.AutoSize = true;
            pnlAppView.BackColor = Color.FromArgb(64, 64, 64);
            pnlAppView.Controls.Add(pnlStatusConsole);
            pnlAppView.Controls.Add(pnlEconomicCalendar);
            pnlAppView.Controls.Add(pnlMarketData);
            pnlAppView.Controls.Add(pnlMarketOutlook);
            pnlAppView.Dock = DockStyle.Fill;
            pnlAppView.Location = new Point(0, 0);
            pnlAppView.Margin = new Padding(2);
            pnlAppView.Name = "pnlAppView";
            pnlAppView.Size = new Size(471, 821);
            pnlAppView.TabIndex = 2;
            // 
            // pnlStatusConsole
            // 
            pnlStatusConsole.Controls.Add(statusConsoleView1);
            pnlStatusConsole.Dock = DockStyle.Fill;
            pnlStatusConsole.Location = new Point(0, 767);
            pnlStatusConsole.Name = "pnlStatusConsole";
            pnlStatusConsole.Size = new Size(471, 54);
            pnlStatusConsole.TabIndex = 1;
            // 
            // statusConsoleView1
            // 
            statusConsoleView1.Dock = DockStyle.Fill;
            statusConsoleView1.Location = new Point(0, 0);
            statusConsoleView1.Margin = new Padding(4);
            statusConsoleView1.Name = "statusConsoleView1";
            statusConsoleView1.Size = new Size(471, 54);
            statusConsoleView1.TabIndex = 0;
            // 
            // pnlEconomicCalendar
            // 
            pnlEconomicCalendar.Controls.Add(economicCalendarView1);
            pnlEconomicCalendar.Dock = DockStyle.Top;
            pnlEconomicCalendar.Location = new Point(0, 496);
            pnlEconomicCalendar.Name = "pnlEconomicCalendar";
            pnlEconomicCalendar.Size = new Size(471, 271);
            pnlEconomicCalendar.TabIndex = 5;
            // 
            // economicCalendarView1
            // 
            economicCalendarView1.Dock = DockStyle.Fill;
            economicCalendarView1.Location = new Point(0, 0);
            economicCalendarView1.Margin = new Padding(4);
            economicCalendarView1.Name = "economicCalendarView1";
            economicCalendarView1.Size = new Size(471, 271);
            economicCalendarView1.TabIndex = 0;
            economicCalendarView1.Load += economicCalendarView1_Load;
            // 
            // pnlMarketData
            // 
            pnlMarketData.Controls.Add(marketDataView1);
            pnlMarketData.Dock = DockStyle.Top;
            pnlMarketData.Location = new Point(0, 300);
            pnlMarketData.Name = "pnlMarketData";
            pnlMarketData.Size = new Size(471, 196);
            pnlMarketData.TabIndex = 3;
            // 
            // marketDataView1
            // 
            marketDataView1.BackColor = Color.Black;
            marketDataView1.Dock = DockStyle.Fill;
            marketDataView1.Location = new Point(0, 0);
            marketDataView1.Margin = new Padding(4);
            marketDataView1.Name = "marketDataView1";
            marketDataView1.Size = new Size(471, 196);
            marketDataView1.TabIndex = 0;
            marketDataView1.Load += marketDataView1_Load;
            // 
            // pnlMarketOutlook
            // 
            pnlMarketOutlook.Controls.Add(marketOutlookView1);
            pnlMarketOutlook.Dock = DockStyle.Top;
            pnlMarketOutlook.Location = new Point(0, 0);
            pnlMarketOutlook.Name = "pnlMarketOutlook";
            pnlMarketOutlook.Size = new Size(471, 300);
            pnlMarketOutlook.TabIndex = 1;
            // 
            // marketOutlookView1
            // 
            marketOutlookView1.BackColor = Color.Black;
            marketOutlookView1.Dock = DockStyle.Fill;
            marketOutlookView1.Location = new Point(0, 0);
            marketOutlookView1.Margin = new Padding(4);
            marketOutlookView1.Name = "marketOutlookView1";
            marketOutlookView1.Size = new Size(471, 300);
            marketOutlookView1.TabIndex = 0;
            // 
            // tradeButton
            // 
            tradeButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            tradeButton.Enabled = false;
            tradeButton.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            tradeButton.Image = (Image)resources.GetObject("tradeButton.Image");
            tradeButton.ImageTransparentColor = Color.Magenta;
            tradeButton.Name = "tradeButton";
            tradeButton.Size = new Size(98, 24);
            tradeButton.Text = "Trade Orders";
            tradeButton.Click += tradeButton_Click;
            // 
            // marketDataButton
            // 
            marketDataButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            marketDataButton.Enabled = false;
            marketDataButton.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            marketDataButton.Image = (Image)resources.GetObject("marketDataButton.Image");
            marketDataButton.ImageTransparentColor = Color.Magenta;
            marketDataButton.Name = "marketDataButton";
            marketDataButton.Size = new Size(95, 24);
            marketDataButton.Text = "Market Data";
            marketDataButton.Click += marketDataButton_Click;
            // 
            // toolStripSeparator
            // 
            toolStripSeparator.Name = "toolStripSeparator";
            toolStripSeparator.Size = new Size(6, 27);
            // 
            // btnCloseOrder
            // 
            btnCloseOrder.Alignment = ToolStripItemAlignment.Right;
            btnCloseOrder.DisplayStyle = ToolStripItemDisplayStyle.Text;
            btnCloseOrder.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnCloseOrder.Image = (Image)resources.GetObject("btnCloseOrder.Image");
            btnCloseOrder.ImageTransparentColor = Color.Magenta;
            btnCloseOrder.Name = "btnCloseOrder";
            btnCloseOrder.Size = new Size(89, 24);
            btnCloseOrder.Text = "Close Order:";
            btnCloseOrder.Visible = false;
            btnCloseOrder.Click += btnCloseOrder_Click;
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(24, 24);
            toolStrip1.Items.AddRange(new ToolStripItem[] { tradeButton, marketDataButton, fundButton, referenceButton, systemAdminButton, toolStripSeparator, btnCloseOrder });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(2637, 27);
            toolStrip1.TabIndex = 0;
            toolStrip1.Text = "toolStrip1";
            // 
            // fundButton
            // 
            fundButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            fundButton.Enabled = false;
            fundButton.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            fundButton.Image = (Image)resources.GetObject("fundButton.Image");
            fundButton.ImageTransparentColor = Color.Magenta;
            fundButton.Name = "fundButton";
            fundButton.Size = new Size(51, 24);
            fundButton.Text = "Funds";
            fundButton.Click += fundButton_Click;
            // 
            // referenceButton
            // 
            referenceButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            referenceButton.Enabled = false;
            referenceButton.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            referenceButton.Image = (Image)resources.GetObject("referenceButton.Image");
            referenceButton.ImageTransparentColor = Color.Magenta;
            referenceButton.Name = "referenceButton";
            referenceButton.Size = new Size(79, 24);
            referenceButton.Text = "Reference";
            referenceButton.Click += referenceButton_Click;
            // 
            // systemAdminButton
            // 
            systemAdminButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            systemAdminButton.Enabled = false;
            systemAdminButton.Font = new Font("Segoe UI", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            systemAdminButton.Image = (Image)resources.GetObject("systemAdminButton.Image");
            systemAdminButton.ImageTransparentColor = Color.Magenta;
            systemAdminButton.Name = "systemAdminButton";
            systemAdminButton.Size = new Size(60, 24);
            systemAdminButton.Text = "System";
            systemAdminButton.Click += systemAdminButton_Click;
            // 
            // statusConsoleLogViewModelBindingSource
            // 
            statusConsoleLogViewModelBindingSource.DataSource = typeof(Shared.StatusConsole.ViewModels.StatusConsoleLogReadModel);
            statusConsoleLogViewModelBindingSource.CurrentChanged += statusConsoleLogViewModelBindingSource_CurrentChanged;
            // 
            // IFMAppView
            // 
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.FromArgb(64, 64, 64);
            ClientSize = new Size(2637, 872);
            Controls.Add(tradeSplitter);
            Controls.Add(statusBar);
            Controls.Add(toolStrip1);
            Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            Name = "IFMAppView";
            Text = "Investment Fund Manager";
            WindowState = FormWindowState.Maximized;
            FormClosing += IFMApp_FormClosing;
            Load += IFMApp_Load;
            Resize += IFMApp_Resize;
            statusBar.ResumeLayout(false);
            statusBar.PerformLayout();
            tradeSplitter.Panel1.ResumeLayout(false);
            tradeSplitter.Panel2.ResumeLayout(false);
            tradeSplitter.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)tradeSplitter).EndInit();
            tradeSplitter.ResumeLayout(false);
            pnlAppView.ResumeLayout(false);
            pnlStatusConsole.ResumeLayout(false);
            pnlEconomicCalendar.ResumeLayout(false);
            pnlMarketData.ResumeLayout(false);
            pnlMarketOutlook.ResumeLayout(false);
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)statusConsoleLogViewModelBindingSource).EndInit();
            ResumeLayout(false);
            PerformLayout();

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