namespace TomasAI.IFM.UI.Net.Views.App;

partial class OperationsView
{
    System.ComponentModel.IContainer components = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    void InitializeComponent()
    {
        pnlTitle = new Panel();
        lblTitle = new Label();
        operationsTabs = new DarkTabControl();
        tabStrategy = new TabPage();
        pnlStrategyHeader = new TableLayoutPanel();
        lblItiStatus = new Label();
        lblTimeFrame = new Label();
        ddlTimeFrame = new ComboBox();
        strategySplitter = new SplitContainer();
        strategyContentSplitter = new SplitContainer();
        itiChart = new System.Windows.Forms.DataVisualization.Charting.Chart();
        lstStrategyWorkflows = new ListView();
        colTime = new ColumnHeader();
        colSignalEvent = new ColumnHeader();
        colTrend = new ColumnHeader();
        colPrice = new ColumnHeader();
        colPipelineState = new ColumnHeader();
        colWorkflowEndState = new ColumnHeader();
        workflowTabs = new DarkTabControl();
        tabWorkflowDetails = new TabPage();
        tabWorkflowSummary = new TabPage();
        workflowDetails = new Strategy.StrategyWorkflowDetailsAccordion();
        lblWorkflowSummaryUnavailable = new Label();
        tabLatency = new TabPage();
        tabTraffic = new TabPage();
        tabErrors = new TabPage();
        tabSaturation = new TabPage();
        pnlTitle.SuspendLayout();
        operationsTabs.SuspendLayout();
        tabStrategy.SuspendLayout();
        pnlStrategyHeader.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)strategySplitter).BeginInit();
        strategySplitter.Panel1.SuspendLayout();
        strategySplitter.Panel2.SuspendLayout();
        strategySplitter.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)strategyContentSplitter).BeginInit();
        strategyContentSplitter.Panel1.SuspendLayout();
        strategyContentSplitter.Panel2.SuspendLayout();
        strategyContentSplitter.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)itiChart).BeginInit();
        SuspendLayout();
        //
        // pnlTitle
        //
        pnlTitle.BackColor = Color.Black;
        pnlTitle.Controls.Add(lblTitle);
        pnlTitle.Dock = DockStyle.Top;
        pnlTitle.Location = new Point(0, 0);
        pnlTitle.Name = "pnlTitle";
        pnlTitle.Size = new Size(527, 25);
        pnlTitle.TabIndex = 0;
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Dock = DockStyle.Left;
        lblTitle.Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lblTitle.ForeColor = Color.White;
        lblTitle.Padding = new Padding(0, 3, 0, 0);
        lblTitle.Text = "Operations";
        //
        // operationsTabs
        //
        operationsTabs.Controls.Add(tabStrategy);
        operationsTabs.Controls.Add(tabLatency);
        operationsTabs.Controls.Add(tabTraffic);
        operationsTabs.Controls.Add(tabErrors);
        operationsTabs.Controls.Add(tabSaturation);
        operationsTabs.BackColor = Color.Black;
        operationsTabs.Dock = DockStyle.Fill;
        operationsTabs.ForeColor = Color.White;
        operationsTabs.Location = new Point(0, 25);
        operationsTabs.Name = "operationsTabs";
        operationsTabs.SelectedIndex = 0;
        operationsTabs.Size = new Size(527, 796);
        operationsTabs.TabIndex = 1;
        operationsTabs.SelectedIndexChanged += operationsTabs_SelectedIndexChanged;
        //
        // tabStrategy
        //
        tabStrategy.BackColor = Color.Black;
        tabStrategy.Controls.Add(strategySplitter);
        tabStrategy.Controls.Add(pnlStrategyHeader);
        tabStrategy.Location = new Point(4, 24);
        tabStrategy.Name = "tabStrategy";
        tabStrategy.Padding = new Padding(3);
        tabStrategy.Text = "Strategy";
        //
        // pnlStrategyHeader
        //
        pnlStrategyHeader.BackColor = Color.Black;
        pnlStrategyHeader.ColumnCount = 3;
        pnlStrategyHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlStrategyHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125F));
        pnlStrategyHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        pnlStrategyHeader.Controls.Add(lblItiStatus, 0, 0);
        pnlStrategyHeader.Controls.Add(lblTimeFrame, 1, 0);
        pnlStrategyHeader.Controls.Add(ddlTimeFrame, 2, 0);
        pnlStrategyHeader.Dock = DockStyle.Top;
        pnlStrategyHeader.Location = new Point(3, 3);
        pnlStrategyHeader.Name = "pnlStrategyHeader";
        pnlStrategyHeader.RowCount = 1;
        pnlStrategyHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlStrategyHeader.Size = new Size(513, 29);
        pnlStrategyHeader.TabIndex = 0;
        //
        // lblItiStatus
        //
        lblItiStatus.Dock = DockStyle.Fill;
        lblItiStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblItiStatus.ForeColor = Color.Silver;
        lblItiStatus.Padding = new Padding(3, 4, 3, 0);
        lblItiStatus.Text = "Intrinsic Time Daily: Not started";
        //
        // lblTimeFrame
        //
        lblTimeFrame.AutoSize = false;
        lblTimeFrame.Dock = DockStyle.Fill;
        lblTimeFrame.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        lblTimeFrame.ForeColor = Color.LightGray;
        lblTimeFrame.Margin = new Padding(3);
        lblTimeFrame.Name = "lblTimeFrame";
        lblTimeFrame.Padding = new Padding(0, 1, 3, 0);
        lblTimeFrame.Text = "Time Frame:";
        lblTimeFrame.TextAlign = ContentAlignment.MiddleRight;
        lblTimeFrame.UseCompatibleTextRendering = false;
        //
        // ddlTimeFrame
        //
        ddlTimeFrame.BackColor = Color.Black;
        ddlTimeFrame.Dock = DockStyle.Fill;
        ddlTimeFrame.DropDownStyle = ComboBoxStyle.DropDownList;
        ddlTimeFrame.FlatStyle = FlatStyle.Flat;
        ddlTimeFrame.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        ddlTimeFrame.ForeColor = Color.White;
        ddlTimeFrame.FormattingEnabled = true;
        ddlTimeFrame.Margin = new Padding(3);
        ddlTimeFrame.Name = "ddlTimeFrame";
        ddlTimeFrame.TabIndex = 1;
        ddlTimeFrame.SelectedIndexChanged += ddlTimeFrame_SelectedIndexChanged;
        //
        // strategySplitter
        //
        strategySplitter.BackColor = Color.Black;
        strategySplitter.Dock = DockStyle.Fill;
        strategySplitter.Location = new Point(3, 32);
        strategySplitter.Name = "strategySplitter";
        strategySplitter.Orientation = Orientation.Horizontal;
        strategySplitter.Panel1.Controls.Add(strategyContentSplitter);
        strategySplitter.Panel1MinSize = 260;
        strategySplitter.Panel2.Controls.Add(workflowTabs);
        strategySplitter.Panel2MinSize = 120;
        strategySplitter.Size = new Size(513, 733);
        strategySplitter.SplitterDistance = 490;
        strategySplitter.SplitterWidth = 5;
        strategySplitter.TabIndex = 1;
        strategySplitter.Resize += strategySplitter_Resize;
        //
        // strategyContentSplitter
        //
        strategyContentSplitter.BackColor = Color.Black;
        strategyContentSplitter.Dock = DockStyle.Fill;
        strategyContentSplitter.Location = new Point(0, 0);
        strategyContentSplitter.Name = "strategyContentSplitter";
        strategyContentSplitter.Orientation = Orientation.Horizontal;
        strategyContentSplitter.Panel1.Controls.Add(itiChart);
        strategyContentSplitter.Panel1MinSize = 120;
        strategyContentSplitter.Panel2.Controls.Add(lstStrategyWorkflows);
        strategyContentSplitter.Panel2MinSize = 120;
        strategyContentSplitter.Size = new Size(513, 490);
        strategyContentSplitter.SplitterDistance = 243;
        strategyContentSplitter.SplitterWidth = 5;
        strategyContentSplitter.TabIndex = 0;
        strategyContentSplitter.Resize += strategyContentSplitter_Resize;
        //
        // itiChart
        //
        itiChart.BackColor = Color.Black;
        itiChart.Dock = DockStyle.Fill;
        itiChart.Location = new Point(0, 0);
        itiChart.Name = "itiChart";
        itiChart.Size = new Size(513, 243);
        itiChart.TabIndex = 0;
        itiChart.MouseClick += itiChart_MouseClick;
        //
        // lstStrategyWorkflows
        //
        lstStrategyWorkflows.BackColor = Color.Black;
        lstStrategyWorkflows.BorderStyle = BorderStyle.None;
        lstStrategyWorkflows.Columns.AddRange([colTime, colSignalEvent, colTrend, colPrice, colPipelineState, colWorkflowEndState]);
        lstStrategyWorkflows.Dock = DockStyle.Fill;
        lstStrategyWorkflows.ForeColor = Color.White;
        lstStrategyWorkflows.FullRowSelect = true;
        lstStrategyWorkflows.HideSelection = false;
        lstStrategyWorkflows.Location = new Point(0, 0);
        lstStrategyWorkflows.MultiSelect = false;
        lstStrategyWorkflows.Name = "lstStrategyWorkflows";
        lstStrategyWorkflows.ShowItemToolTips = true;
        lstStrategyWorkflows.Size = new Size(513, 242);
        lstStrategyWorkflows.TabIndex = 1;
        lstStrategyWorkflows.UseCompatibleStateImageBehavior = false;
        lstStrategyWorkflows.View = View.Details;
        lstStrategyWorkflows.SelectedIndexChanged += lstStrategyWorkflows_SelectedIndexChanged;
        colTime.Text = "Date/Time";
        colTime.Width = 185;
        colSignalEvent.Text = "Futures ITI Signal Event";
        colSignalEvent.Width = 165;
        colTrend.Text = "Trend Type";
        colTrend.Width = 90;
        colPrice.Text = "Futures Price";
        colPrice.Width = 85;
        colPipelineState.Text = "Pipeline State";
        colPipelineState.Width = 235;
        colWorkflowEndState.Text = "Workflow End State";
        colWorkflowEndState.Width = 135;
        //
        // workflowTabs
        //
        workflowTabs.BackColor = Color.Black;
        workflowTabs.Controls.Add(tabWorkflowDetails);
        workflowTabs.Controls.Add(tabWorkflowSummary);
        workflowTabs.Dock = DockStyle.Fill;
        workflowTabs.ForeColor = Color.White;
        workflowTabs.Name = "workflowTabs";
        workflowTabs.SelectedIndex = 0;
        workflowTabs.TabIndex = 0;
        //
        // tabWorkflowDetails
        //
        tabWorkflowDetails.BackColor = Color.Black;
        tabWorkflowDetails.Controls.Add(workflowDetails);
        tabWorkflowDetails.Name = "tabWorkflowDetails";
        tabWorkflowDetails.Text = "Details";
        tabWorkflowDetails.UseVisualStyleBackColor = false;
        //
        // workflowDetails
        //
        workflowDetails.Dock = DockStyle.Fill;
        workflowDetails.Name = "workflowDetails";
        //
        // tabWorkflowSummary
        //
        tabWorkflowSummary.BackColor = Color.Black;
        tabWorkflowSummary.Controls.Add(lblWorkflowSummaryUnavailable);
        tabWorkflowSummary.Name = "tabWorkflowSummary";
        tabWorkflowSummary.Text = "Summary";
        tabWorkflowSummary.UseVisualStyleBackColor = false;
        //
        // lblWorkflowSummaryUnavailable
        //
        lblWorkflowSummaryUnavailable.BackColor = Color.Black;
        lblWorkflowSummaryUnavailable.Dock = DockStyle.Fill;
        lblWorkflowSummaryUnavailable.ForeColor = Color.Silver;
        lblWorkflowSummaryUnavailable.Name = "lblWorkflowSummaryUnavailable";
        lblWorkflowSummaryUnavailable.Text = "Summary is not available.";
        lblWorkflowSummaryUnavailable.TextAlign = ContentAlignment.MiddleCenter;
        //
        // placeholders
        //
        ConfigurePlaceholder(tabLatency, "Latency", "Latency operations are not implemented yet.");
        ConfigurePlaceholder(tabTraffic, "Traffic", "Traffic operations are not implemented yet.");
        ConfigurePlaceholder(tabErrors, "Errors", "Error operations are not implemented yet.");
        ConfigurePlaceholder(tabSaturation, "Saturation", "Saturation operations are not implemented yet.");
        //
        // OperationsView
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.Black;
        Controls.Add(operationsTabs);
        Controls.Add(pnlTitle);
        Name = "OperationsView";
        Size = new Size(527, 821);
        pnlTitle.ResumeLayout(false);
        pnlTitle.PerformLayout();
        operationsTabs.ResumeLayout(false);
        pnlStrategyHeader.ResumeLayout(false);
        tabStrategy.ResumeLayout(false);
        strategySplitter.Panel1.ResumeLayout(false);
        strategySplitter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)strategySplitter).EndInit();
        strategySplitter.ResumeLayout(false);
        strategyContentSplitter.Panel1.ResumeLayout(false);
        strategyContentSplitter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)strategyContentSplitter).EndInit();
        strategyContentSplitter.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)itiChart).EndInit();
        ResumeLayout(false);
    }

    static void ConfigurePlaceholder(TabPage page, string title, string message)
    {
        page.BackColor = Color.Black;
        page.Text = title;
        var label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Silver,
            Text = message,
            TextAlign = ContentAlignment.MiddleCenter
        };
        page.Controls.Add(label);
    }

    Panel pnlTitle = null!;
    Label lblTitle = null!;
    TabControl operationsTabs = null!;
    TabPage tabStrategy = null!;
    TabPage tabLatency = null!;
    TabPage tabTraffic = null!;
    TabPage tabErrors = null!;
    TabPage tabSaturation = null!;
    Label lblItiStatus = null!;
    Label lblTimeFrame = null!;
    ComboBox ddlTimeFrame = null!;
    TableLayoutPanel pnlStrategyHeader = null!;
    SplitContainer strategySplitter = null!;
    SplitContainer strategyContentSplitter = null!;
    System.Windows.Forms.DataVisualization.Charting.Chart itiChart = null!;
    ListView lstStrategyWorkflows = null!;
    ColumnHeader colTime = null!;
    ColumnHeader colSignalEvent = null!;
    ColumnHeader colTrend = null!;
    ColumnHeader colPrice = null!;
    ColumnHeader colPipelineState = null!;
    ColumnHeader colWorkflowEndState = null!;
    TabControl workflowTabs = null!;
    TabPage tabWorkflowDetails = null!;
    TabPage tabWorkflowSummary = null!;
    Strategy.StrategyWorkflowDetailsAccordion workflowDetails = null!;
    Label lblWorkflowSummaryUnavailable = null!;
}
