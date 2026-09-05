using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Portfolio-centric administration and Fund navigation. Legacy Funds remains a separate shell entry.</summary>
public sealed class PortfolioAdministrationForm : Form, IForm<PortfolioAdministrationForm>
{
    readonly DataGridView _portfolios = PortfolioUiStyle.Grid("Portfolio list");
    readonly DataGridView _funds = PortfolioUiStyle.Grid("Funds in selected Portfolio");
    readonly DataGridView _allocation = PortfolioUiStyle.Grid("Current Fund allocation");
    readonly DataGridView _envelope = PortfolioUiStyle.Grid("Current Fund risk envelope");
    readonly DataGridView _assignments = PortfolioUiStyle.Grid("Fund trade template assignments");
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.White, BackColor = Color.Black, AccessibleName = "Portfolio operation status", Padding = new Padding(6) };
    readonly ComboBox _state = PortfolioUiStyle.Combo("Portfolio state filter");
    readonly Button _refresh = PortfolioUiStyle.Button("Refresh", "Refresh Portfolios");
    readonly Button _createPortfolio = PortfolioUiStyle.Button("New Portfolio...", "Create Portfolio");
    readonly Button _riskPolicy = PortfolioUiStyle.Button("Risk Policy...", "Manage Portfolio Risk Policy");
    readonly Button _portfolioActions = PortfolioUiStyle.Button("Portfolio Actions", "Portfolio actions menu");
    readonly Button _newPortfolioVersion = PortfolioUiStyle.Button("New Portfolio Version...", "Create Portfolio version");
    readonly Button _portfolioState = PortfolioUiStyle.Button("Change Portfolio State...", "Change Portfolio state");
    readonly Button _deletePortfolio = PortfolioUiStyle.Button("Delete Draft...", "Delete Draft Portfolio");
    readonly Button _createFund = PortfolioUiStyle.Button("Create Fund...", "Create Fund mandate");
    readonly Button _newFundVersion = PortfolioUiStyle.Button("New Fund Version...", "Create Fund mandate version");
    readonly Button _fundState = PortfolioUiStyle.Button("Change Fund State...", "Change Fund state");
    readonly Button _configureAllocation = PortfolioUiStyle.Button("Allocation...", "Configure Fund allocation");
    readonly Button _configureEnvelope = PortfolioUiStyle.Button("Risk Envelope...", "Configure Fund risk envelope");
    readonly Button _configureAssignment = PortfolioUiStyle.Button("Trade Assignment...", "Configure Fund trade assignment");
    readonly ContextMenuStrip _portfolioActionsMenu = new();
    readonly Label _menuTitle = PortfolioUiStyle.MenuTitle("Portfolio Administration");
    readonly FlowLayoutPanel _menuBar = new()
    {
        Name = "portfolioMenuBar",
        AccessibleName = "Portfolio menu bar",
        Dock = DockStyle.Top,
        Height = 54,
        Padding = new Padding(6),
        BackColor = Color.Black,
        ForeColor = Color.White,
        AutoScroll = true,
        WrapContents = false,
    };
    readonly Panel _contentFrame = new()
    {
        Name = "portfolioContentFrame",
        AccessibleName = "Portfolio administration border",
        Dock = DockStyle.Fill,
        BackColor = PortfolioUiStyle.Border,
        Padding = new Padding(PortfolioUiStyle.BorderWidth),
    };
    PortfolioAdministrationViewModel? _viewModel;
    IPortfolioQueryApi? _queries;
    IPortfolioFinancialPolicyCommandApi? _policyCommands;
    IPortfolioIdentityApi? _identities;
    IReferenceQueryApi? _referenceQueries;
    CancellationTokenSource? _load;
    bool _bindingSelection;
    long _portfolioSelectionGeneration;
    long _fundSelectionGeneration;

    public PortfolioAdministrationForm()
    {
        Text = "Portfolio Administration"; Name = "PortfolioAdministrationForm"; AccessibleName = "Portfolio Administration";
        Width = 1450; Height = 900; MinimumSize = new(1100, 700); PortfolioUiStyle.Apply(this);
        _state.Width = 140; _state.Dock = DockStyle.None;
        _state.Items.AddRange(Enum.GetValues<PortfolioOperatingState>().Where(x => x != PortfolioOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = PortfolioOperatingState.Active;
        _menuBar.Controls.Add(_menuTitle);
        _menuBar.Controls.Add(new Label { Text = "Show State", AutoSize = true, BackColor = PortfolioUiStyle.MenuSurface, ForeColor = PortfolioUiStyle.Foreground, Padding = new Padding(0, 11, 0, 0) });
        _menuBar.Controls.AddRange([_state, _refresh, _createPortfolio, _riskPolicy, _portfolioActions]);
        _portfolioActionsMenu.Items.Add("New Version...", null, async (_, _) => await NewPortfolioVersionAsync());
        _portfolioActionsMenu.Items.Add("Change State...", null, async (_, _) => await ChangePortfolioStateAsync());
        _portfolioActionsMenu.Items.Add("Delete Draft...", null, async (_, _) => await DeleteDraftPortfolioAsync());
        var fundToolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface, AutoScroll = true, WrapContents = false };
        fundToolbar.Controls.AddRange([_createFund, _newFundVersion, _fundState, _configureAllocation, _configureEnvelope, _configureAssignment]);
        var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Selected Portfolio and Fund details", BackColor = PortfolioUiStyle.Surface, ForeColor = PortfolioUiStyle.Foreground };
        tabs.TabPages.Add(Page("Funds", _funds)); tabs.TabPages.Add(Page("Allocation", _allocation)); tabs.TabPages.Add(Page("Risk Envelope", _envelope)); tabs.TabPages.Add(Page("Trade Assignments", _assignments));
        var bottom = new Panel { Dock = DockStyle.Fill, BackColor = PortfolioUiStyle.Surface }; bottom.Controls.Add(tabs); bottom.Controls.Add(fundToolbar);
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 340, BackColor = PortfolioUiStyle.Surface };
        split.Panel1.BackColor = PortfolioUiStyle.Surface; split.Panel2.BackColor = PortfolioUiStyle.Surface;
        split.Panel1.Controls.Add(_portfolios); split.Panel2.Controls.Add(bottom);
        _contentFrame.Controls.Add(split); _contentFrame.Controls.Add(_menuBar); _contentFrame.Controls.Add(_status);
        Controls.Add(_contentFrame);
        SetSelectionButtons();
        _refresh.Click += async (_, _) => await RefreshAsync(); _state.SelectedIndexChanged += async (_, _) => await RefreshAsync();
        // SelectionChanged can still expose the previous CurrentRow during a cell change.
        _portfolios.CurrentCellChanged += async (_, _) => await SelectPortfolioAsync();
        _funds.CurrentCellChanged += async (_, _) => await SelectFundAsync();
        _portfolioActions.Click += (_, _) => _portfolioActionsMenu.Show(_portfolioActions, new Point(0, _portfolioActions.Height));
        _riskPolicy.Click += (_, _) => ShowRiskPolicy(); _createPortfolio.Click += async (_, _) => await CreatePortfolioAsync();
        _newPortfolioVersion.Click += async (_, _) => await NewPortfolioVersionAsync(); _portfolioState.Click += async (_, _) => await ChangePortfolioStateAsync();
        _deletePortfolio.Click += async (_, _) => await DeleteDraftPortfolioAsync();
        _createFund.Click += async (_, _) => await CreateFundAsync(); _newFundVersion.Click += async (_, _) => await NewFundVersionAsync(); _fundState.Click += async (_, _) => await ChangeFundStateAsync();
        _configureAllocation.Click += async (_, _) => await ConfigureAllocationAsync(); _configureEnvelope.Click += async (_, _) => await ConfigureEnvelopeAsync(); _configureAssignment.Click += async (_, _) => await ConfigureAssignmentAsync();
        FormClosed += (_, _) => { _viewModel?.ClearSelection(); _load?.Cancel(); _load?.Dispose(); };
    }

    public async Task LoadViewModelAsync(IPortfolioQueryApi queries, IPortfolioCommandApi commands, IPortfolioFundCommandApi fundCommands, IPortfolioIdentityApi identities, IPortfolioFinancialPolicyCommandApi? policyCommands = null, IReferenceQueryApi? referenceQueries = null, bool canMutate = true)
    {
        _queries = queries; _policyCommands = policyCommands; _identities = identities; _referenceQueries = referenceQueries;
        _viewModel = new(queries, commands, fundCommands, identities, canMutate); SetSelectionButtons(); await RefreshAsync();
    }

    async Task RefreshAsync()
    {
        if (_viewModel is null || _state.SelectedItem is not PortfolioOperatingState state) return;
        _load?.Cancel(); _load?.Dispose(); var load = _load = new();
        _portfolioSelectionGeneration++; _fundSelectionGeneration++;
        _viewModel.ClearSelection();
        _bindingSelection = true;
        try { _portfolios.DataSource = null; _funds.DataSource = null; BindConfiguration(); }
        finally { _bindingSelection = false; }
        SetSelectionButtons(); _status.Text = "Loading Portfolios...";
        try
        {
            await _viewModel.LoadAsync(state, load.Token);
            if (IsDisposed || Disposing || load != _load) return;
            _bindingSelection = true;
            try { _portfolios.DataSource = _viewModel.Portfolios; }
            finally { _bindingSelection = false; }
            ShowStatus();
            await SelectPortfolioAsync();
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed && !Disposing && load == _load) _status.Text = "Portfolio refresh cancelled.";
        }
    }

    async Task SelectPortfolioAsync()
    {
        if (_bindingSelection || _viewModel is null || IsDisposed || Disposing) return;
        var generation = ++_portfolioSelectionGeneration;
        _fundSelectionGeneration++;
        _bindingSelection = true;
        try { _funds.DataSource = null; }
        finally { _bindingSelection = false; }
        if (_portfolios.CurrentRow?.DataBoundItem is not PortfolioReadModel portfolio)
        {
            _viewModel.ClearSelection(); BindConfiguration(); SetSelectionButtons(); return;
        }
        try
        {
            var selection = _viewModel.SelectPortfolioAsync(portfolio, _load?.Token ?? default);
            BindConfiguration(); SetSelectionButtons();
            await selection;
            if (IsDisposed || Disposing || generation != _portfolioSelectionGeneration) return;
            _bindingSelection = true;
            try { _funds.DataSource = _viewModel.Funds; }
            finally { _bindingSelection = false; }
            BindConfiguration();
            ShowStatus(_viewModel.State == PortfolioUiState.Ready
                ? $"Portfolio {portfolio.PortfolioId}: {_viewModel.Funds.Length} Fund mandate(s)." : null);
            SetSelectionButtons();
            await SelectFundAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing && generation == _portfolioSelectionGeneration)
                ShowStatus($"Unable to load Portfolio {portfolio.PortfolioId}: {exception.Message}");
        }
    }

    async Task SelectFundAsync()
    {
        if (_bindingSelection || _viewModel is null || IsDisposed || Disposing) return;
        var generation = ++_fundSelectionGeneration;
        if (_funds.CurrentRow?.DataBoundItem is not FundMandateReadModel fund
            || _viewModel.SelectedPortfolio?.PortfolioId != fund.PortfolioId)
        {
            _viewModel.ClearFundSelection(); BindConfiguration(); SetSelectionButtons(); return;
        }
        try
        {
            var selection = _viewModel.SelectFundAsync(fund, _load?.Token ?? default);
            BindConfiguration(); SetSelectionButtons();
            await selection;
            if (IsDisposed || Disposing || generation != _fundSelectionGeneration) return;
            BindConfiguration();
            ShowStatus(_viewModel.State == PortfolioUiState.Ready
                ? $"Fund {fund.FundId} version {fund.FundMandateVersion} selected." : null);
            SetSelectionButtons();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception)
        {
            if (!IsDisposed && !Disposing && generation == _fundSelectionGeneration)
                ShowStatus($"Unable to load Fund {fund.FundId}: {exception.Message}");
        }
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

    async Task DeleteDraftPortfolioAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { OperatingState: PortfolioOperatingState.Draft } selected) return;
        using var dialog = new DeleteDraftPortfolioDialog(selected);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (await _viewModel.DeleteDraftPortfolioAsync(dialog.Reason)) await RefreshForStateAsync(PortfolioOperatingState.Draft);
        else ShowStatus();
    }

    async Task CreateFundAsync()
    {
        if (_viewModel?.SelectedPortfolio is not { } portfolio) return;
        var catalog = await LoadTradeFamilyCatalogAsync(); if (catalog is null) return;
        _status.Text = "Allocating Fund ID..."; var id = await _viewModel.AllocateFundIdAsync(); if (id is null) { ShowStatus(); return; }
        using var editor = new FundMandateEditorForm(portfolio.PortfolioId, id.Value, catalog: catalog); if (editor.ShowDialog(this) != DialogResult.OK || editor.Value is null) return;
        await _viewModel.CreateFundAsync(editor.Value); ShowStatus(); await SelectPortfolioAsync();
    }

    async Task NewFundVersionAsync()
    {
        if (_viewModel?.SelectedFund is not { } selected) return;
        var catalog = await LoadTradeFamilyCatalogAsync(); if (catalog is null) return;
        using var editor = new FundMandateEditorForm(selected.PortfolioId, selected.FundId, selected, catalog);
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
        if (_viewModel?.SelectedPortfolio is not { } portfolio || _viewModel.SelectedFund is not { } fund) return;
        var catalog = await LoadTradeFamilyCatalogAsync(); if (catalog is null) return;
        using var editor = new FundAssignmentEditorForm(portfolio, fund, catalog);
        if (editor.ShowDialog(this) == DialogResult.OK && editor.Value is not null) { await _viewModel.AssignTradeTemplateAsync(editor.Value); ShowStatus(); await SelectFundAsync(); }
    }

    async Task<TradeStrategyFamilyReadModel[]?> LoadTradeFamilyCatalogAsync()
    {
        if (_referenceQueries is null) { ShowStatus("Trade strategy family catalog is unavailable: Reference queries are not connected."); return null; }
        ShowStatus("Loading trade strategy families...");
        try
        {
            var result = await _referenceQueries.GetTradeStrategyFamiliesAsync();
            if (IsDisposed || Disposing) return null;
            if (!result.Success || result.Value is null)
            {
                ShowStatus($"Unable to load trade strategy families: {result.ErrorMessage ?? "no catalog returned"}");
                return null;
            }
            var active = TradeFamilyCatalogSelection.Active(result.Value);
            if (active.Length == 0) { ShowStatus("No active trade strategy families are available. Cannot open the editor."); return null; }
            ShowStatus("Trade strategy family catalog loaded.");
            return active;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing) ShowStatus($"Unable to load trade strategy families: {ex.Message}");
            return null;
        }
    }

    async Task RefreshForStateAsync(PortfolioOperatingState state) { _state.SelectedItem = state; await RefreshAsync(); }
    void BindConfiguration() { _allocation.DataSource = _viewModel?.Allocation is null ? Array.Empty<FundAllocationReadModel>() : new[] { _viewModel.Allocation }; _envelope.DataSource = _viewModel?.RiskEnvelope is null ? Array.Empty<FundRiskEnvelopeReadModel>() : new[] { _viewModel.RiskEnvelope }; _assignments.DataSource = _viewModel?.Assignments ?? []; }
    void ShowStatus(string? message = null) { _status.Text = message ?? (_viewModel?.State == PortfolioUiState.Empty ? "No Portfolios match the filter." : _viewModel?.Message) ?? string.Empty; }
    void ShowRiskPolicy() { if (_viewModel?.SelectedPortfolio is not { } portfolio || _queries is null || _identities is null) return; using var form = new PortfolioRiskPolicyForm(portfolio, _queries, _identities, _policyCommands, _referenceQueries, _viewModel.CanMutate); form.ShowDialog(this); }
    void SetSelectionButtons() { var can = _viewModel?.CanMutate == true && _viewModel.State != PortfolioUiState.Loading; var portfolio = _viewModel?.SelectedPortfolio is not null; var draft = _viewModel?.SelectedPortfolio?.OperatingState == PortfolioOperatingState.Draft; var fund = _viewModel?.SelectedFund is not null; _createPortfolio.Enabled = can; _riskPolicy.Enabled = portfolio; _portfolioActions.Enabled = can && portfolio; _newPortfolioVersion.Enabled = can && portfolio; _portfolioState.Enabled = can && portfolio; _deletePortfolio.Enabled = can && draft; if (_portfolioActionsMenu.Items.Count == 3) _portfolioActionsMenu.Items[2].Enabled = can && draft; _createFund.Enabled = can && portfolio; _newFundVersion.Enabled = can && fund; _fundState.Enabled = can && fund; _configureAllocation.Enabled = can && fund; _configureEnvelope.Enabled = can && fund; _configureAssignment.Enabled = can && fund; }
    static TabPage Page(string title, Control content) { var page = new TabPage(title) { BackColor = PortfolioUiStyle.Surface, ForeColor = PortfolioUiStyle.Foreground }; page.Controls.Add(content); return page; }
}

public sealed class DeleteDraftPortfolioDialog : Form
{
    readonly TextBox _confirmation = PortfolioUiStyle.TextBox("Portfolio ID confirmation");
    readonly TextBox _reason = PortfolioUiStyle.TextBox("Draft deletion reason");
    readonly Button _delete = PortfolioUiStyle.Button("Delete Draft", "Confirm Draft Portfolio deletion");
    readonly string _portfolioId;

    public DeleteDraftPortfolioDialog(PortfolioReadModel portfolio)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        if (portfolio.OperatingState != PortfolioOperatingState.Draft) throw new ArgumentException("Only a Draft Portfolio can be confirmed for deletion.", nameof(portfolio));
        _portfolioId = portfolio.PortfolioId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Text = "Delete Draft Portfolio"; Name = "DeleteDraftPortfolioDialog"; AccessibleName = Text;
        Width = 660; Height = 320; MinimizeBox = false; MaximizeBox = false; PortfolioUiStyle.Apply(this);
        var warning = PortfolioUiStyle.Caption($"Delete Draft Portfolio {portfolio.PortfolioId}: {portfolio.Name}\r\nThe sequence-generated ID will never be reused. Type '{_portfolioId}' to confirm.");
        warning.Dock = DockStyle.Fill; warning.ForeColor = Color.MistyRose;
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface };
        body.ColumnStyles.Add(new(SizeType.Absolute, 190)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        body.Controls.Add(warning, 0, 0); body.SetColumnSpan(warning, 2);
        body.Controls.Add(PortfolioUiStyle.Caption("Portfolio ID"), 0, 1); body.Controls.Add(_confirmation, 1, 1);
        body.Controls.Add(PortfolioUiStyle.Caption("Reason"), 0, 2); body.Controls.Add(_reason, 1, 2);
        var cancel = PortfolioUiStyle.Button("Cancel", "Cancel Draft Portfolio deletion");
        _delete.Enabled = false; _confirmation.TextChanged += (_, _) => EnableDelete(); _reason.TextChanged += (_, _) => EnableDelete();
        _delete.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); }; cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(_delete); Controls.Add(body); Controls.Add(buttons); CancelButton = cancel;
    }

    public string Reason => _reason.Text.Trim();
    void EnableDelete() => _delete.Enabled = string.Equals(_confirmation.Text.Trim(), _portfolioId, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_reason.Text);
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
