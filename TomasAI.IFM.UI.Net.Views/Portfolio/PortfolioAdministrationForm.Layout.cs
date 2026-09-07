using System.ComponentModel;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed partial class PortfolioAdministrationForm
{
    readonly DataGridView _fundSummary = PortfolioUiStyle.Grid("Selected Fund information");
    readonly TableLayoutPanel _sections = new() { Name = "portfolioSections", Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
    readonly TableLayoutPanel _metricStrip = new() { Name = "fundMetrics", Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 2, Height = 64, Margin = Padding.Empty };
    readonly TextBox[] _metricValues = new TextBox[10];
    readonly Label _metricStatus = new() { AutoEllipsis = true, Dock = DockStyle.Fill, Height = 25, Text = "Select a Fund to view metrics.", AccessibleName = "Fund metrics status" };
    readonly DarkDateTimePicker _metricsFrom = new() { Name = "metricsFrom", AccessibleName = "Fund metrics from date", Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 125, Value = new DateTime(DateTime.Today.Year, 1, 1) };
    readonly DarkDateTimePicker _metricsTo = new() { Name = "metricsTo", AccessibleName = "Fund metrics through date", Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 125, Value = DateTime.Today };
    readonly ToolTip _metricTips = new();
    FundMetricsViewModel? _metrics;

    void BuildAdministrationLayout()
    {
        _menuBar.Controls.Clear();
        _menuBar.Controls.Add(_menuTitle);
        _menuBar.Controls.Add(new Label { Text = "Metrics from", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        _menuBar.Controls.Add(_metricsFrom);
        _menuBar.Controls.Add(new Label { Text = "to", AutoSize = true, Margin = new Padding(3, 8, 3, 3) });
        _menuBar.Controls.Add(_metricsTo);
        var reload = PortfolioUiStyle.Button("Refresh Metrics", "Refresh selected Fund metrics");
        _menuBar.Controls.Add(reload);
        reload.Click += async (_, _) => await LoadMetricsAsync();
        _metricsFrom.ValueChanged += async (_, _) => await LoadMetricsAsync();
        _metricsTo.ValueChanged += async (_, _) => await LoadMetricsAsync();

        ConfigureList(_portfolios, "PortfolioVersion");
        ConfigureList(_funds, "FundMandateVersion");
        var tabs = new DarkTabControl { Dock = DockStyle.Fill, AccessibleName = "Selected Fund details", Padding = new Point(6, 4), ShowToolTips = true };
        tabs.TabPages.Add(Page("Fund", _fundSummary));
        tabs.TabPages.Add(Page("Allocation", _allocation));
        tabs.TabPages.Add(Page("Risk", _envelope));
        tabs.TabPages.Add(Page("Assignments", _assignments));
        tabs.TabPages[2].ToolTipText = "Fund Risk Envelope";
        tabs.TabPages[3].ToolTipText = "Fund Trade Assignments";
        for (var index = 0; index < 3; index++) _sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        _sections.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _sections.Controls.Add(Section("Portfolios", _portfolios,
            [new Label { Text = "Show State", AutoSize = true, Margin = new Padding(4, 8, 4, 4) }, _state, _refresh, _createPortfolio, _riskPolicy, _portfolioActions]), 0, 0);
        _sections.Controls.Add(Section("Funds", _funds, [_createFund, _newFundVersion, _fundState]), 1, 0);
        _sections.Controls.Add(Section("Selected Fund Details", tabs, [_configureAllocation, _configureEnvelope, _configureAssignment]), 2, 0);

        string[] labels = ["Win Rate", "Avg Profit", "Loss Rate", "Avg Loss", "W/L Ratio", "Sharpe Ratio", "P&L", "P&L (%)", "Commission", "Max DD (%)"];
        _metricStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _metricStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        for (var index = 0; index < labels.Length; index++)
        {
            _metricStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
            var label = new Label { Text = labels[index], UseMnemonic = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(2), AutoEllipsis = true };
            var value = PortfolioUiStyle.TextBox(labels[index] + " selected Fund metric", true);
            value.Text = "N/A"; value.TextAlign = HorizontalAlignment.Center; value.Margin = new Padding(2);
            _metricValues[index] = value;
            _metricStrip.Controls.Add(label, index, 0); _metricStrip.Controls.Add(value, index, 1);
        }
        _metricTips.SetToolTip(_metricValues[9], "Maximum recorded balance drawdown. Includes deposits and withdrawals; excludes unrecorded unrealized P&L.");
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        _status.Dock = DockStyle.Fill;
        body.Controls.Add(_sections, 0, 0); body.Controls.Add(_status, 0, 1);
        body.Controls.Add(_metricStatus, 0, 2); body.Controls.Add(_metricStrip, 0, 3);
        _contentFrame.Controls.Add(body); _contentFrame.Controls.Add(_menuBar);
    }

    static Control Section(string title, Control content, Control[] actions)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = new Padding(3), Padding = new Padding(3), ColumnCount = 1, RowCount = 3, CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(PortfolioUiStyle.BodyFont, FontStyle.Bold) }, 0, 0);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = Padding.Empty };
        toolbar.Controls.AddRange(actions);
        panel.Controls.Add(toolbar, 0, 1); panel.Controls.Add(content, 0, 2);
        return panel;
    }

    static void ConfigureList(DataGridView grid, string versionProperty)
    {
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 90 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OperatingState", HeaderText = "State", Width = 80, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = versionProperty, HeaderText = "Version", Width = 65, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
    }

    void BindFundSummary()
    {
        var fund = _viewModel?.SelectedFund;
        _fundSummary.DataSource = fund is null ? Array.Empty<DetailRow>() : new DetailRow[]
        {
            new("Fund ID", fund.FundId.ToString()), new("Name", fund.Name),
            new("Version", fund.FundMandateVersion.ToString()), new("Trading year", fund.TradingYear.ToString()),
            new("State", fund.OperatingState.ToString()), new("Effective from", fund.EffectiveFromUtc.ToString("yyyy-MM-dd")),
            new("Effective until", fund.EffectiveUntilUtc?.ToString("yyyy-MM-dd") ?? "Open ended"),
            new("Decision horizon", fund.DecisionHorizon), new("Objective", fund.Objective),
            new("Underlyings", string.Join(", ", fund.UnderlyingUniverse)), new("Asset types", string.Join(", ", fund.EligibleAssetTypes)),
            new("Directions", string.Join(", ", fund.PermittedDirections)), new("Conditions", string.Join(", ", fund.PermittedConditions)),
            new("Strategy permissions", $"{fund.PermittedTradeStrategyFamilies.Length} configured; see Assignments")
        };
        ConfigureDetails(_fundSummary);
    }

    static void BindDetails(DataGridView grid, object? value)
    {
        grid.DataSource = Details(value);
        ConfigureDetails(grid);
    }

    static void ConfigureDetails(DataGridView grid)
    {
        grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
    }

    sealed record DetailRow(string Property, string Value);

    static DetailRow[] Details(object? value) => value is null ? [] : TypeDescriptor.GetProperties(value).Cast<PropertyDescriptor>()
        .Select(property => new DetailRow(property.DisplayName, property.GetValue(value) is string[] items ? string.Join(", ", items) : property.GetValue(value)?.ToString() ?? "")).ToArray();

    Task LoadMetricsAsync()
        => _metrics is not null && _viewModel?.SelectedFund is { } fund
            ? _metrics.LoadAsync(fund.FundId, DateOnly.FromDateTime(_metricsFrom.Value), DateOnly.FromDateTime(_metricsTo.Value))
            : Task.CompletedTask;

    void RenderMetrics()
    {
        if (IsDisposed || Disposing) return;
        foreach (var value in _metricValues) value.Text = "N/A";
        _metricStatus.Text = _metrics?.Message ?? "Fund metrics unavailable.";
        if (_metrics?.Report is not { HasHistory: true } report) return;
        string[] values = [report.WinRate.ToString("P2"), report.AverageProfit.ToString("N2"), report.LossRate.ToString("P2"), report.AverageLoss.ToString("N2"), report.WinLossRatio.ToString("F2"), report.ActualSharpeRatio.ToString("F2"), report.PnlAmount.ToString("N2"), report.PnlPercent.ToString("P2"), report.TradeCommission.ToString("N2"), report.MaximumDrawdownPercent?.ToString("P2") ?? "N/A"];
        for (var index = 0; index < values.Length; index++) _metricValues[index].Text = values[index];
        var currency = _viewModel?.RiskEnvelope?.Currency ?? _viewModel?.SelectedPortfolio?.BaseCurrency ?? "";
        _metricStatus.Text += $" Amounts: {currency}.";
        _metricTips.SetToolTip(_metricValues[9], $"Maximum recorded balance drawdown: {report.MaximumDrawdownAmount?.ToString("N2") ?? "N/A"} {currency}. Includes cash flows; excludes unrecorded unrealized P&L.");
    }
}
