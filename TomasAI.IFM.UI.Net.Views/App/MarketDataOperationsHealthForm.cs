using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Views.App;

/// <summary>Independent read-only central operations status; it owns no market subscription or reset authority.</summary>
public sealed class MarketDataOperationsHealthForm : DarkTradingForm, IForm<MarketDataOperationsHealthForm>
{
    readonly MarketDataOperationsHealthViewModel viewModel;
    readonly CancellationTokenSource closing = new();
    readonly System.Windows.Forms.Timer timer = new() { Interval = 5000 };
    readonly Label summary = Label("operationsHealthSummary", 48);
    readonly Label observation = Label("operationsHealthObservation", 44);
    readonly Button refresh = new() { Name = "refreshOperationsHealth", Text = "Refresh status", AutoSize = true };
    readonly DataGridView stages = Grid("operationsStageGrid");
    readonly DataGridView datasets = Grid("operationsDatasetGrid");
    bool closeComplete;
    bool disposed;

    public MarketDataOperationsHealthForm(IMarketDataOperationsHealthQueryService service)
    {
        viewModel = new(service);
        Name = "MarketDataOperationsHealthForm";
        Text = "Market Data Operations Health (read-only)";
        ClientSize = new Size(1160, 700);
        MinimumSize = new Size(900, 540);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(25, 25, 25);
        ForeColor = Color.Gainsboro;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        var explanation = Label("operationsHealthExplanation", 54);
        explanation.Text = "Source age describes the market-data timestamp; stage status describes processing and recovery. "
            + "An old source timestamp alone does not prove a queue is stalled. Inactive means monitoring is not required.";
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        refresh.BackColor = Color.FromArgb(60, 60, 60);
        refresh.ForeColor = Color.White;
        toolbar.Controls.Add(refresh);
        toolbar.Controls.Add(new Label
        {
            AutoSize = true, Text = "Auto-refresh every 5 seconds; status queries only.", Padding = new Padding(8)
        });
        var tabs = new TomasAI.IFM.UI.Net.Views.App.DarkTabControl { Name = "operationsHealthTabs", Dock = DockStyle.Fill };
        var stageTab = new TabPage("Processing stages") { BackColor = BackColor };
        var datasetTab = new TabPage("Dataset workers / recovery") { BackColor = BackColor };
        stageTab.Controls.Add(stages);
        datasetTab.Controls.Add(datasets);
        tabs.TabPages.Add(stageTab);
        tabs.TabPages.Add(datasetTab);
        var footer = Label("operationsHealthReadOnlyNotice", 42);
        footer.Text = "Recovery remains controlled by the central lifecycle owner. This panel cannot reset datasets or replace workers. "
            + "Timestamps below are explicitly UTC; scroll horizontally for additional metrics.";
        layout.Controls.Add(summary, 0, 0);
        layout.Controls.Add(observation, 0, 1);
        layout.Controls.Add(explanation, 0, 2);
        layout.Controls.Add(toolbar, 0, 3);
        layout.Controls.Add(tabs, 0, 4);
        layout.Controls.Add(footer, 0, 5);
        Controls.Add(layout);
        refresh.Click += Refresh_Click;
        timer.Tick += Refresh_Click;
        Shown += Form_Shown;
        FormClosing += Form_Closing;
        Render();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, closing.Token);
        refresh.Enabled = false;
        try
        {
            await viewModel.RefreshAsync(linked.Token);
            if (!IsDisposed && !closing.IsCancellationRequested) Render();
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        finally
        {
            if (!IsDisposed && !closing.IsCancellationRequested) refresh.Enabled = true;
        }
    }

    async void Form_Shown(object? sender, EventArgs args)
    {
        await RefreshAsync();
        if (!closing.IsCancellationRequested) timer.Start();
    }

    async void Refresh_Click(object? sender, EventArgs args) => await RefreshAsync();

    async void Form_Closing(object? sender, FormClosingEventArgs args)
    {
        if (closeComplete) return;
        args.Cancel = true;
        timer.Stop();
        closing.Cancel();
        await viewModel.DisposeAsync();
        closeComplete = true;
        Close();
    }

    void Render()
    {
        summary.Text = viewModel.Summary;
        summary.AccessibleName = summary.Text;
        summary.ForeColor = StatusColor(viewModel.Status);
        observation.Text = viewModel.Observation;
        stages.DataSource = viewModel.Stages.ToList();
        datasets.DataSource = viewModel.Datasets.ToList();
        FormatRows(stages);
        FormatRows(datasets);
    }

    static void FormatRows(DataGridView grid)
    {
        foreach (DataGridViewColumn column in grid.Columns)
        {
            column.MinimumWidth = column.Name is "Reason" or "ReasonCode" ? 160 : 85;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        if (grid.Columns.Count > 1)
        {
            grid.Columns[0].Frozen = true;
            grid.Columns[1].Frozen = true;
        }
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (grid.Columns.Contains("Status"))
                row.Cells["Status"].Style.ForeColor = StatusColor(row.Cells["Status"].Value?.ToString() ?? "Unknown");
            if (grid.Columns.Contains("Reason"))
                foreach (DataGridViewCell cell in row.Cells)
                    cell.ToolTipText = row.Cells["Reason"].Value?.ToString() ?? string.Empty;
        }
        grid.AccessibleName = grid.Name == "operationsStageGrid" ? "Central processing stage health" : "Dataset worker and recovery health";
    }

    static Color StatusColor(string status) => status.ToUpperInvariant() switch
    {
        "GREEN" or "UP" or "HEALTHY" => Color.LightGreen,
        "YELLOW" or "SUSPECT" => Color.Khaki,
        "ORANGE" or "RECOVERING" or "RESETTING" or "QUALIFYING" => Color.Orange,
        "RED" or "DOWN" or "FAILED" => Color.Salmon,
        _ => Color.Silver
    };

    static Label Label(string name, int height) => new()
    {
        Name = name, Dock = DockStyle.Fill, Height = height, TextAlign = ContentAlignment.MiddleLeft,
        AutoEllipsis = true, Margin = new Padding(0, 2, 0, 2)
    };

    static DataGridView Grid(string name) => new()
    {
        Name = name, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, AllowUserToOrderColumns = true, RowHeadersVisible = false,
        AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
        BackgroundColor = Color.FromArgb(25, 25, 25), GridColor = Color.DimGray,
        EnableHeadersVisualStyles = false, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(50, 50, 50), ForeColor = Color.White },
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.Gainsboro },
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            timer.Stop();
            timer.Dispose();
            closing.Cancel();
            viewModel.Cancel();
            closing.Dispose();
        }
        base.Dispose(disposing);
    }
}
