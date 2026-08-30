using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Portfolio-centric administration and Fund navigation. Legacy Funds remains a separate shell entry.</summary>
public sealed class PortfolioAdministrationForm : Form
{
    readonly DataGridView _portfolios = PortfolioUiStyle.Grid("Portfolio list");
    readonly DataGridView _funds = PortfolioUiStyle.Grid("Funds in selected Portfolio");
    readonly DataGridView _allocation = PortfolioUiStyle.Grid("Current Fund allocation");
    readonly DataGridView _envelope = PortfolioUiStyle.Grid("Current Fund risk envelope");
    readonly DataGridView _assignments = PortfolioUiStyle.Grid("Fund trade template assignments");
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.White, BackColor = Color.Black, AccessibleName = "Portfolio operation status", Padding = new Padding(6) };
    readonly ComboBox _state = PortfolioUiStyle.Combo("Portfolio state filter");
    readonly Button _refresh = PortfolioUiStyle.Button("Refresh", "Refresh Portfolios");
    readonly Button _createPortfolio = PortfolioUiStyle.Button("Create Portfolio...", "Create Portfolio");
    readonly Button _newPortfolioVersion = PortfolioUiStyle.Button("New Portfolio Version...", "Create Portfolio version");
    readonly Button _portfolioState = PortfolioUiStyle.Button("Change Portfolio State...", "Change Portfolio state");
    readonly Button _createFund = PortfolioUiStyle.Button("Create Fund...", "Create Fund mandate");
    readonly Button _newFundVersion = PortfolioUiStyle.Button("New Fund Version...", "Create Fund mandate version");
    readonly Button _fundState = PortfolioUiStyle.Button("Change Fund State...", "Change Fund state");
    readonly Button _configureAllocation = PortfolioUiStyle.Button("Allocation...", "Configure Fund allocation");
    readonly Button _configureEnvelope = PortfolioUiStyle.Button("Risk Envelope...", "Configure Fund risk envelope");
    readonly Button _configureAssignment = PortfolioUiStyle.Button("Trade Assignment...", "Configure Fund trade assignment");
    readonly Button _compositions = PortfolioUiStyle.Button("Planned Compositions", "View planned compositions");
    PortfolioAdministrationViewModel? _viewModel;
    IPortfolioQueryApi? _queries;
    CancellationTokenSource? _load;

    public PortfolioAdministrationForm()
    {
        Text = "Portfolio Administration"; Name = "PortfolioAdministrationForm"; AccessibleName = "Portfolio Administration";
        Width = 1450; Height = 900; MinimumSize = new(1100, 700); PortfolioUiStyle.Apply(this);
        _state.Width = 140; _state.Dock = DockStyle.None;
        _state.Items.AddRange(Enum.GetValues<PortfolioOperatingState>().Where(x => x != PortfolioOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = PortfolioOperatingState.Active;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6), BackColor = Color.Black, AutoScroll = true, WrapContents = false };
        toolbar.Controls.Add(new Label { Text = "State", AutoSize = true, Padding = new Padding(0, 11, 0, 0), ForeColor = Color.White });
        toolbar.Controls.AddRange([_state, _refresh, _createPortfolio, _newPortfolioVersion, _portfolioState, _compositions]);
        var fundToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface, AutoScroll = true, WrapContents = false };
        fundToolbar.Controls.AddRange([_createFund, _newFundVersion, _fundState, _configureAllocation, _configureEnvelope, _configureAssignment]);
        var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Selected Portfolio and Fund details" };
        tabs.TabPages.Add(Page("Funds", _funds)); tabs.TabPages.Add(Page("Allocation", _allocation)); tabs.TabPages.Add(Page("Risk Envelope", _envelope)); tabs.TabPages.Add(Page("Trade Assignments", _assignments));
        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = PortfolioUiStyle.Surface }; bottom.Controls.Add(tabs); bottom.Controls.Add(fundToolbar);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 340, BackColor = PortfolioUiStyle.Surface };
        split.Panel1.Controls.Add(_portfolios); split.Panel2.Controls.Add(bottom);
        Controls.Add(split); Controls.Add(toolbar); Controls.Add(_status);
        SetSelectionButtons();
        _refresh.Click += async (_, _) => await RefreshAsync(); _state.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        _portfolios.SelectionChanged += async (_, _) => await SelectPortfolioAsync(); _funds.SelectionChanged += async (_, _) => await SelectFundAsync();
        _compositions.Click += (_, _) => ShowCompositions(); _createPortfolio.Click += async (_, _) => await CreatePortfolioAsync();
        _newPortfolioVersion.Click += async (_, _) => await NewPortfolioVersionAsync(); _portfolioState.Click += async (_, _) => await ChangePortfolioStateAsync();
        _createFund.Click += async (_, _) => await CreateFundAsync(); _newFundVersion.Click += async (_, _) => await NewFundVersionAsync(); _fundState.Click += async (_, _) => await ChangeFundStateAsync();
        _configureAllocation.Click += async (_, _) => await ConfigureAllocationAsync(); _configureEnvelope.Click += async (_, _) => await ConfigureEnvelopeAsync(); _configureAssignment.Click += async (_, _) => await ConfigureAssignmentAsync();
        FormClosed += (_, _) => { _load?.Cancel(); _load?.Dispose(); };
    }

    public async Task LoadViewModelAsync(IPortfolioQueryApi queries, IPortfolioCommandApi commands, IPortfolioFundCommandApi fundCommands, IPortfolioIdentityApi identities, bool canMutate = true)
    {
        _queries = queries; _viewModel = new(queries, commands, fundCommands, identities, canMutate); SetSelectionButtons(); await RefreshAsync();
    }

    async Task RefreshAsync()
    {
        if (_viewModel is null || _state.SelectedItem is not PortfolioOperatingState state) return;
        _load?.Cancel(); _load?.Dispose(); _load = new(); _status.Text = "Loading Portfolios...";
        try { await _viewModel.LoadAsync(state, _load.Token); _portfolios.DataSource = _viewModel.Portfolios; ShowStatus(); }
        catch (OperationCanceledException) { _status.Text = "Portfolio refresh cancelled."; }
        SetSelectionButtons();
    }

    async Task SelectPortfolioAsync()
    {
        if (_viewModel is null || _portfolios.CurrentRow?.DataBoundItem is not PortfolioReadModel portfolio) { SetSelectionButtons(); return; }
        await _viewModel.SelectPortfolioAsync(portfolio); _funds.DataSource = _viewModel.Funds; BindConfiguration(); ShowStatus($"Portfolio {portfolio.PortfolioId}: {_viewModel.Funds.Length} Fund mandate(s)."); SetSelectionButtons();
    }

    async Task SelectFundAsync()
    {
        if (_viewModel is null || _funds.CurrentRow?.DataBoundItem is not FundMandateReadModel fund) { SetSelectionButtons(); return; }
        await _viewModel.SelectFundAsync(fund); BindConfiguration(); ShowStatus($"Fund {fund.FundId} version {fund.FundMandateVersion} selected."); SetSelectionButtons();
    }

    async Task CreatePortfolioAsync()
    {
        if (_viewModel is null) return; _status.Text = "Allocating Portfolio ID...";
        var allocated = await _viewModel.AllocatePortfolioIdAsync();
        if (!allocated.IsSuccessful || allocated.PortfolioId is null) { ShowStatus(allocated.Error); return; }
        using var editor = new PortfolioEditorForm(allocated.PortfolioId.Id); if (editor.ShowDialog(this) != DialogResult.OK || editor.Value is null) return;
        if (await _viewModel.CreatePortfolioAsync(editor.Value)) await RefreshForStateAsync(editor.Value.OperatingState); else ShowStatus();
    }

    async Task NewPortfolioVersionAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } selected) return; using var editor = new PortfolioEditorForm(selected.PortfolioId, selected);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.AddPortfolioVersionAsync(editor.Value, selected.PortfolioVersion); ShowStatus(); await RefreshAsync(); }
    }

    async Task ChangePortfolioStateAsync()
    {
        if (_viewModel?.SelectedPortfolio is null) return; using var dialog = new StateReasonDialog<PortfolioOperatingState>("Change Portfolio State", _viewModel.SelectedPortfolio.OperatingState);
        if (dialog.ShowDialog(this) == DialogResult.OK) { await _viewModel.ChangePortfolioStateAsync(dialog.State, dialog.Reason); ShowStatus(); await RefreshForStateAsync(dialog.State); }
    }

    async Task CreateFundAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } portfolio) return; _status.Text = "Allocating Fund ID..."; var id = await _viewModel.AllocateFundIdAsync(); if (id is null) { ShowStatus(); return; }
        using var editor = new FundMandateEditorForm(portfolio.PortfolioId, id.Value); if (editor.ShowDialog(this) != DialogResult.OK || editor.Value is null) return;
        await _viewModel.CreateFundAsync(editor.Value); ShowStatus(); await SelectPortfolioAsync();
    }

    async Task NewFundVersionAsync()
    {
        if (_viewModel?.SelectedFund is not { } selected) return; using var editor = new FundMandateEditorForm(selected.PortfolioId, selected.FundId, selected);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.AddFundVersionAsync(editor.Value, selected.FundMandateVersion); ShowStatus(); await SelectPortfolioAsync(); }
    }

    async Task ChangeFundStateAsync()
    {
        if (_viewModel?.SelectedFund is null) return; using var dialog = new StateReasonDialog<FundOperatingState>("Change Fund State", _viewModel.SelectedFund.OperatingState);
        if (dialog.ShowDialog(this) == DialogResult.OK) { await _viewModel.ChangeFundStateAsync(dialog.State, dialog.Reason); ShowStatus(); await SelectPortfolioAsync(); }
    }

    async Task ConfigureAllocationAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } portfolio || _viewModel.SelectedFund is not { } fund) return; using var editor = new FundAllocationEditorForm(portfolio, fund, _viewModel.Allocation);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.DelegateAllocationAsync(editor.Value); ShowStatus(); await SelectFundAsync(); }
    }

    async Task ConfigureEnvelopeAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } portfolio || _viewModel.SelectedFund is not { } fund) return; using var editor = new FundRiskEnvelopeEditorForm(portfolio, fund, _viewModel.RiskEnvelope);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.DelegateRiskEnvelopeAsync(editor.Value); ShowStatus(); await SelectFundAsync(); }
    }

    async Task ConfigureAssignmentAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } portfolio || _viewModel.SelectedFund is not { } fund) return; using var editor = new FundAssignmentEditorForm(portfolio, fund);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.AssignTradeTemplateAsync(editor.Value); ShowStatus(); await SelectFundAsync(); }
    }

    async Task RefreshForStateAsync(PortfolioOperatingState state) { _state.SelectedItem = state; await RefreshAsync(); }
    void BindConfiguration() { _allocation.DataSource = _viewModel?.Allocation is null ? Array.Empty<FundAllocationReadModel>() : new[] { _viewModel.Allocation }; _envelope.DataSource = _viewModel?.RiskEnvelope is null ? Array.Empty<FundRiskEnvelopeReadModel>() : new[] { _viewModel.RiskEnvelope }; _assignments.DataSource = _viewModel?.Assignments ?? []; }
    void ShowStatus(string? message = null) { _status.Text = message ?? (_viewModel?.State == PortfolioUiState.Empty ? "No Portfolios match the filter." : _viewModel?.Message) ?? string.Empty; }
    void ShowCompositions() { if (_queries is null || _viewModel?.SelectedPortfolio is not { } portfolio) return; using var form = new PortfolioCompositionForm(_queries, portfolio); form.ShowDialog(this); }
    void SetSelectionButtons() { var can = _viewModel?.CanMutate == true; var portfolio = _viewModel?.SelectedPortfolio is not null; var fund = _viewModel?.SelectedFund is not null; _createPortfolio.Enabled = can; _newPortfolioVersion.Enabled = can && portfolio; _portfolioState.Enabled = can && portfolio; _createFund.Enabled = can && portfolio; _newFundVersion.Enabled = can && fund; _fundState.Enabled = can && fund; _configureAllocation.Enabled = can && fund; _configureEnvelope.Enabled = can && fund; _configureAssignment.Enabled = can && fund; _compositions.Enabled = portfolio; }
    static TabPage Page(string title, Control content) { var page = new TabPage(title) { BackColor = PortfolioUiStyle.Surface, ForeColor = PortfolioUiStyle.Foreground }; page.Controls.Add(content); return page; }
}

sealed class StateReasonDialog<TState> : Form where TState : struct, Enum
{
    readonly ComboBox _state = PortfolioUiStyle.Combo("Target state"); readonly TextBox _reason = PortfolioUiStyle.TextBox("State change reason");
    public StateReasonDialog(string title, TState current)
    {
        Text = title; Width = 600; Height = 250; PortfolioUiStyle.Apply(this); _state.Items.AddRange(Enum.GetValues<TState>().Where(x => Convert.ToInt32(x) != 0).Cast<object>().ToArray()); _state.SelectedItem = current;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) }; body.ColumnStyles.Add(new(SizeType.Absolute, 180)); body.ColumnStyles.Add(new(SizeType.Percent, 100)); body.Controls.Add(PortfolioUiStyle.Caption("Target State"), 0, 0); body.Controls.Add(_state, 1, 0); body.Controls.Add(PortfolioUiStyle.Caption("Reason"), 0, 1); body.Controls.Add(_reason, 1, 1);
        var ok = PortfolioUiStyle.Button("Apply", "Apply state change"); var cancel = PortfolioUiStyle.Button("Cancel", "Cancel state change"); ok.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_reason.Text)) return; DialogResult = DialogResult.OK; Close(); }; cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(cancel); buttons.Controls.Add(ok); Controls.Add(body); Controls.Add(buttons); AcceptButton = ok; CancelButton = cancel;
    }
    public TState State => (TState)(_state.SelectedItem ?? default(TState)); public string Reason => _reason.Text.Trim();
}
