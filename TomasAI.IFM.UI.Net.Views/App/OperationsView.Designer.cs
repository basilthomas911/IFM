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
        operationsTabs = new TabControl();
        tabStrategy = new TabPage();
        strategySplitter = new SplitContainer();
        lstItiEvents = new ListView();
        colTime = new ColumnHeader();
        colPeriod = new ColumnHeader();
        colMode = new ColumnHeader();
        colTrend = new ColumnHeader();
        colPrice = new ColumnHeader();
        itiPropertyGrid = new PropertyGrid();
        lblItiStatus = new Label();
        tabLatency = new TabPage();
        tabTraffic = new TabPage();
        tabErrors = new TabPage();
        tabSaturation = new TabPage();
        pnlTitle.SuspendLayout();
        operationsTabs.SuspendLayout();
        tabStrategy.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)strategySplitter).BeginInit();
        strategySplitter.Panel1.SuspendLayout();
        strategySplitter.Panel2.SuspendLayout();
        strategySplitter.SuspendLayout();
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
        operationsTabs.Dock = DockStyle.Fill;
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
        tabStrategy.Controls.Add(lblItiStatus);
        tabStrategy.Location = new Point(4, 24);
        tabStrategy.Name = "tabStrategy";
        tabStrategy.Padding = new Padding(3);
        tabStrategy.Text = "Strategy";
        //
        // strategySplitter
        //
        strategySplitter.BackColor = Color.Black;
        strategySplitter.Dock = DockStyle.Fill;
        strategySplitter.Location = new Point(3, 30);
        strategySplitter.Name = "strategySplitter";
        strategySplitter.Orientation = Orientation.Horizontal;
        strategySplitter.Panel1.Controls.Add(lstItiEvents);
        strategySplitter.Panel1MinSize = 180;
        strategySplitter.Panel2.Controls.Add(itiPropertyGrid);
        strategySplitter.Panel2MinSize = 120;
        strategySplitter.Size = new Size(513, 735);
        strategySplitter.SplitterDistance = 490;
        strategySplitter.SplitterWidth = 5;
        strategySplitter.TabIndex = 1;
        strategySplitter.Resize += strategySplitter_Resize;
        //
        // lblItiStatus
        //
        lblItiStatus.Dock = DockStyle.Top;
        lblItiStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblItiStatus.ForeColor = Color.Silver;
        lblItiStatus.Padding = new Padding(3, 4, 3, 0);
        lblItiStatus.Size = new Size(513, 27);
        lblItiStatus.Text = "ITI: Not started";
        //
        // lstItiEvents
        //
        lstItiEvents.BackColor = Color.Black;
        lstItiEvents.BorderStyle = BorderStyle.None;
        lstItiEvents.Columns.AddRange([colTime, colPeriod, colMode, colTrend, colPrice]);
        lstItiEvents.Dock = DockStyle.Fill;
        lstItiEvents.ForeColor = Color.White;
        lstItiEvents.FullRowSelect = true;
        lstItiEvents.HideSelection = false;
        lstItiEvents.Location = new Point(0, 0);
        lstItiEvents.MultiSelect = false;
        lstItiEvents.Name = "lstItiEvents";
        lstItiEvents.Size = new Size(513, 490);
        lstItiEvents.TabIndex = 1;
        lstItiEvents.UseCompatibleStateImageBehavior = false;
        lstItiEvents.View = View.Details;
        lstItiEvents.SelectedIndexChanged += lstItiEvents_SelectedIndexChanged;
        colTime.Text = "Time";
        colTime.Width = 125;
        colPeriod.Text = "Period";
        colPeriod.Width = 58;
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
        tabStrategy.ResumeLayout(false);
        strategySplitter.Panel1.ResumeLayout(false);
        strategySplitter.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)strategySplitter).EndInit();
        strategySplitter.ResumeLayout(false);
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
    SplitContainer strategySplitter = null!;
    ListView lstItiEvents = null!;
    ColumnHeader colTime = null!;
    ColumnHeader colPeriod = null!;
    ColumnHeader colMode = null!;
    ColumnHeader colTrend = null!;
    ColumnHeader colPrice = null!;
    PropertyGrid itiPropertyGrid = null!;
}
