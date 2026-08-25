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
        lstItiEvents = new ListView();
        colTime = new ColumnHeader();
        colMode = new ColumnHeader();
        colTrend = new ColumnHeader();
        colPrice = new ColumnHeader();
        itiPropertyGrid = new PropertyGrid();
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
        strategySplitter.Panel2.Controls.Add(itiPropertyGrid);
        strategySplitter.Panel2MinSize = 120;
        strategySplitter.Size = new Size(513, 733);
        strategySplitter.SplitterDistance = 490;
        strategySplitter.SplitterWidth = 5;
        strategySplitter.TabIndex = 1;
        strategySplitter.Resize += strategySplitter_Resize;
        //
        // strategyContentSplitter
        //
        strategyContentSplitter.BackColor = Color.FromArgb(64, 64, 64);
        strategyContentSplitter.Dock = DockStyle.Fill;
        strategyContentSplitter.Location = new Point(0, 0);
        strategyContentSplitter.Name = "strategyContentSplitter";
        strategyContentSplitter.Orientation = Orientation.Horizontal;
        strategyContentSplitter.Panel1.Controls.Add(itiChart);
        strategyContentSplitter.Panel1MinSize = 120;
        strategyContentSplitter.Panel2.Controls.Add(lstItiEvents);
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
        // lstItiEvents
        //
        lstItiEvents.BackColor = Color.Black;
        lstItiEvents.BorderStyle = BorderStyle.None;
        lstItiEvents.Columns.AddRange([colTime, colMode, colTrend, colPrice]);
        lstItiEvents.Dock = DockStyle.Fill;
        lstItiEvents.ForeColor = Color.White;
        lstItiEvents.FullRowSelect = true;
        lstItiEvents.HideSelection = false;
        lstItiEvents.Location = new Point(0, 0);
        lstItiEvents.MultiSelect = false;
        lstItiEvents.Name = "lstItiEvents";
        lstItiEvents.Size = new Size(513, 242);
        lstItiEvents.TabIndex = 1;
        lstItiEvents.UseCompatibleStateImageBehavior = false;
        lstItiEvents.View = View.Details;
        lstItiEvents.SelectedIndexChanged += lstItiEvents_SelectedIndexChanged;
        colTime.Text = "Time";
        colTime.Width = 185;
        colMode.Text = "Change";
        colMode.Width = 132;
        colTrend.Text = "Trend";
        colTrend.Width = 75;
        colPrice.Text = "Price";
        colPrice.Width = 85;
        //
        // itiPropertyGrid
        //
        itiPropertyGrid.BackColor = Color.Black;
        itiPropertyGrid.CategoryForeColor = Color.White;
        itiPropertyGrid.CommandsBackColor = Color.Black;
        itiPropertyGrid.CommandsForeColor = Color.White;
        itiPropertyGrid.Dock = DockStyle.Fill;
        itiPropertyGrid.HelpBackColor = Color.Black;
        itiPropertyGrid.HelpForeColor = Color.White;
        itiPropertyGrid.HelpVisible = false;
        itiPropertyGrid.LineColor = Color.FromArgb(64, 64, 64);
        itiPropertyGrid.Location = new Point(0, 0);
        itiPropertyGrid.Name = "itiPropertyGrid";
        itiPropertyGrid.PropertySort = PropertySort.NoSort;
        itiPropertyGrid.Size = new Size(513, 240);
        itiPropertyGrid.TabIndex = 0;
        itiPropertyGrid.ToolbarVisible = false;
        itiPropertyGrid.ViewBackColor = Color.Black;
        itiPropertyGrid.ViewForeColor = Color.White;
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
    ListView lstItiEvents = null!;
    ColumnHeader colTime = null!;
    ColumnHeader colMode = null!;
    ColumnHeader colTrend = null!;
    ColumnHeader colPrice = null!;
    PropertyGrid itiPropertyGrid = null!;
}
