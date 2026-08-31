using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Bounded v1 editor for immutable PortfolioFinancialPolicy versions.</summary>
public sealed class PortfolioRiskPolicyForm : Form
{
    readonly PortfolioReadModel _portfolio;
    readonly IPortfolioQueryApi _queries;
    readonly IPortfolioIdentityApi _identities;
    readonly IPortfolioFinancialPolicyCommandApi? _commands;
    readonly IReferenceQueryApi? _references;
    readonly bool _canMutate;
    readonly DataGridView _policies = PortfolioUiStyle.Grid("Financial policy versions");
    readonly DataGridView _families = PortfolioUiStyle.Grid("Trade strategy family limits");
    readonly Label _header = PortfolioUiStyle.Caption(string.Empty);
    readonly Label _status = PortfolioUiStyle.Caption("Loading policies...");
    readonly TextBox _name = PortfolioUiStyle.TextBox("Policy name");
    readonly TextBox _currency = PortfolioUiStyle.TextBox("Policy base currency", true);
    readonly NumericUpDown _capital = Number();
    readonly NumericUpDown _reserve = Number();
    readonly NumericUpDown _deployable = Number();
    readonly NumericUpDown _perTrade = Number();
    readonly NumericUpDown _aggregate = Number();
    readonly NumericUpDown _margin = Number();
    readonly NumericUpDown _notional = Number();
    readonly NumericUpDown _positions = Number(100000);
    readonly NumericUpDown _drawdown = Number();
    readonly Button _newPolicy = PortfolioUiStyle.Button("New Policy", "Allocate and create a financial policy");
    readonly Button _newVersion = PortfolioUiStyle.Button("New Version", "Create an immutable policy version");
    readonly Button _activate = PortfolioUiStyle.Button("Activate && Assign", "Activate and assign selected policy");
    readonly Button _retire = PortfolioUiStyle.Button("Retire", "Retire selected policy");
    readonly Button _delete = PortfolioUiStyle.Button("Delete Draft", "Delete selected Draft policy");
    readonly Button _save = PortfolioUiStyle.Button("Save", "Save the edited financial policy Draft");
    readonly Button _cancel = PortfolioUiStyle.Button("Cancel", "Discard financial policy edits");
    readonly Func<bool> _confirmDiscard;
    TradeStrategyFamilyReadModel[] _catalog = [];
    PortfolioFinancialPolicyReadModel? _selected;
    PortfolioFinancialPolicyReadModel? _editingPolicy;
    bool _binding;
    bool _dirty;
    bool _editing;
    bool _editingNewVersion;

    public PortfolioRiskPolicyForm(PortfolioReadModel portfolio, IPortfolioQueryApi queries, IPortfolioIdentityApi identities,
        IPortfolioFinancialPolicyCommandApi? commands, IReferenceQueryApi? references, bool canMutate, Func<bool>? confirmDiscard = null)
    {
        _portfolio = portfolio; _queries = queries; _identities = identities; _commands = commands; _references = references; _canMutate = canMutate;
        _confirmDiscard = confirmDiscard ?? (() => MessageBox.Show(this, "Discard unsaved Risk Policy changes?", "Unsaved Risk Policy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes);
        Text = "Portfolio Risk Policy"; Name = "PortfolioRiskPolicyForm"; AccessibleName = Text;
        Width = 1260; Height = 820; MinimumSize = new(1000, 680); PortfolioUiStyle.Apply(this);
        _header.Text = $"Portfolio {portfolio.PortfolioId} — {portfolio.Name}    Active policy: {(portfolio.ActivePolicyId > 0 ? $"{portfolio.ActivePolicyId} v{portfolio.ActivePolicyVersion}" : "No policy assigned")}";
        _header.Dock = DockStyle.Top; _header.Height = 38;
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, BackColor = PortfolioUiStyle.Surface, Padding = new Padding(5) };
        actions.Controls.AddRange([_newPolicy, _newVersion, _save, _cancel, _activate, _retire, _delete]);
        var global = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new Padding(8), BackColor = PortfolioUiStyle.Surface };
        global.ColumnStyles.Add(new(SizeType.Absolute, 170)); global.ColumnStyles.Add(new(SizeType.Percent, 50)); global.ColumnStyles.Add(new(SizeType.Absolute, 180)); global.ColumnStyles.Add(new(SizeType.Percent, 50));
        Add(global, 0, 0, "Name", _name); Add(global, 0, 2, "Base Currency", _currency);
        Add(global, 1, 0, "Capital Base", _capital); Add(global, 1, 2, "Protected Reserve", _reserve);
        Add(global, 2, 0, "Deployable Capital", _deployable); Add(global, 2, 2, "Risk Per Trade", _perTrade);
        Add(global, 3, 0, "Aggregate Risk", _aggregate); Add(global, 3, 2, "Maximum Margin", _margin);
        Add(global, 4, 0, "Gross Notional", _notional); Add(global, 4, 2, "Open Positions / Drawdown", Pair(_positions, _drawdown));
        var details = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 300 };
        details.Panel1.Controls.Add(global); details.Panel2.Controls.Add(_families);
        var body = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 390 };
        body.Panel1.Controls.Add(_policies); body.Panel2.Controls.Add(details);
        _status.Dock = DockStyle.Bottom; _status.Height = 34;
        Controls.Add(body); Controls.Add(actions); Controls.Add(_header); Controls.Add(_status);
        _policies.SelectionChanged += (_, _) => BindSelected();
        _families.ReadOnly = true;
        _families.DataBindingComplete += (_, _) =>
        {
            if (_families.Columns[nameof(TradeFamilyRiskLimitReadModel.TradeStrategyFamilyId)] is { } familyId) familyId.ReadOnly = true;
            if (_families.Columns[nameof(TradeFamilyRiskLimitReadModel.DefinitionVersion)] is { } version) version.ReadOnly = true;
        };
        _newPolicy.Click += async (_, _) => await BeginNewPolicyAsync();
        _newVersion.Click += (_, _) => BeginNewVersion();
        _save.Click += async (_, _) => await SaveAsync();
        _cancel.Click += (_, _) => CancelEdit();
        _activate.Click += async (_, _) => await ActivateAsync();
        _retire.Click += async (_, _) => await RetireAsync();
        _delete.Click += async (_, _) => await DeleteAsync();
        foreach (var text in new[] { _name }) text.TextChanged += (_, _) => MarkDirty();
        foreach (var number in Numbers()) number.ValueChanged += (_, _) => MarkDirty();
        _families.CellValueChanged += (_, _) => MarkDirty();
        _families.CurrentCellDirtyStateChanged += (_, _) => { if (_families.IsCurrentCellDirty) _families.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        FormClosing += (_, e) => { if (_editing && _dirty && !_confirmDiscard()) e.Cancel = true; };
        Shown += async (_, _) => await RefreshAsync();
        SetActions();
    }

    public bool HasUnsavedChanges => _editing && _dirty;

    async Task RefreshAsync()
    {
        try
        {
            if (_references is not null)
            {
                var catalog = await _references.GetTradeStrategyFamiliesAsync();
                if (catalog.Success && catalog.Value is not null) _catalog = catalog.Value;
            }
            var result = await _queries.GetPoliciesAsync(_portfolio.PortfolioId, 200);
            _policies.DataSource = result.Success && result.Value is not null ? result.Value.Items : [];
            _status.Text = result.Success ? $"{result.Value?.Items.Length ?? 0} policy version(s)." : result.ErrorMessage;
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        SetActions();
    }

    void BindSelected()
    {
        if (_editing) return;
        _selected = _policies.CurrentRow?.DataBoundItem as PortfolioFinancialPolicyReadModel;
        if (_selected is null) { SetActions(); return; }
        DisplayPolicy(_selected);
    }

    async Task BeginNewPolicyAsync()
    {
        if (!_canMutate || _commands is null) return;
        var allocation = await _identities.AllocatePolicyIdAsync();
        if (!allocation.Success || allocation.Value is null) { _status.Text = allocation.ErrorMessage; return; }
        var now = DateTime.UtcNow;
        BeginEdit(new PortfolioFinancialPolicyReadModel
        {
            PortfolioId = _portfolio.PortfolioId, PolicyId = allocation.Value.Value, PolicyVersion = 1, OperatingState = PortfolioFinancialPolicyState.Draft,
            BaseCurrency = _portfolio.BaseCurrency, TradeFamilyLimits = [.. _catalog.Select(x => new TradeFamilyRiskLimitReadModel { TradeStrategyFamilyId = x.TradeStrategyFamilyId, DefinitionVersion = x.DefinitionVersion })],
            EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = Environment.UserName,
        }, false);
        _status.Text = $"Editing new policy {allocation.Value.Value}. The sequence ID is consumed even if editing is cancelled.";
    }

    void BeginNewVersion()
    {
        if (!_canMutate || _commands is null || _selected is null) return;
        var now = DateTime.UtcNow;
        BeginEdit(_selected.DefensiveCopy() with
        {
            PolicyVersion = _selected.PolicyVersion + 1, OperatingState = PortfolioFinancialPolicyState.Draft,
            EffectiveFromUtc = now, EffectiveUntilUtc = null, CreatedOnUtc = now, CreatedBy = Environment.UserName,
            SupersededOnUtc = null, SupersededBy = string.Empty,
        }, true);
        _status.Text = $"Editing immutable policy version {_editingPolicy!.PolicyVersion}.";
    }

    async Task SaveAsync()
    {
        if (!_editing || _editingPolicy is null || _commands is null) return;
        _families.EndEdit();
        var policy = BuildPolicy();
        var errors = policy.Validate(); if (errors.Count != 0) { _status.Text = string.Join("; ", errors); return; }
        var result = _editingNewVersion
            ? await _commands.AddPolicyVersionAsync(policy, _selected!.AggregateRevision)
            : await _commands.CreatePolicyAsync(policy, Guid.NewGuid());
        _status.Text = result.Success ? "Policy command accepted; refreshing projection..." : result.ErrorMessage;
        if (result.Success) { _editing = false; _dirty = false; _editingPolicy = null; await RefreshAsync(); }
        SetActions();
    }

    void CancelEdit() { if (!_editing) return; _editing = false; _dirty = false; _editingPolicy = null; if (_selected is not null) DisplayPolicy(_selected); else SetActions(); _status.Text = "Policy edits discarded; allocated sequence IDs remain consumed."; }

    void BeginEdit(PortfolioFinancialPolicyReadModel policy, bool version) { _editing = true; _editingNewVersion = version; _editingPolicy = policy; DisplayPolicy(policy); _dirty = false; SetActions(); }

    PortfolioFinancialPolicyReadModel BuildPolicy()
    {
        var source = _editingPolicy ?? throw new InvalidOperationException("No policy is being edited.");
        var limits = _families.DataSource is IEnumerable<TradeFamilyRiskLimitReadModel> rows ? rows.ToArray() : source.TradeFamilyLimits;
        return source with
        {
            Name = _name.Text.Trim(), BaseCurrency = _portfolio.BaseCurrency, CapitalBase = _capital.Value,
            ProtectedReserve = _reserve.Value, MaximumDeployableCapital = _deployable.Value, MaximumRiskPerTrade = _perTrade.Value,
            MaximumAggregateRisk = _aggregate.Value, MaximumMargin = _margin.Value, MaximumGrossNotional = _notional.Value,
            MaximumOpenPositions = (int)_positions.Value, MaximumDrawdownAmount = _drawdown.Value, TradeFamilyLimits = [.. limits]
        };
    }

    void DisplayPolicy(PortfolioFinancialPolicyReadModel policy)
    {
        _binding = true;
        try
        {
            _name.Text = policy.Name; _currency.Text = policy.BaseCurrency; _capital.Value = Fit(_capital, policy.CapitalBase);
            _reserve.Value = Fit(_reserve, policy.ProtectedReserve); _deployable.Value = Fit(_deployable, policy.MaximumDeployableCapital);
            _perTrade.Value = Fit(_perTrade, policy.MaximumRiskPerTrade); _aggregate.Value = Fit(_aggregate, policy.MaximumAggregateRisk);
            _margin.Value = Fit(_margin, policy.MaximumMargin); _notional.Value = Fit(_notional, policy.MaximumGrossNotional);
            _positions.Value = Fit(_positions, policy.MaximumOpenPositions); _drawdown.Value = Fit(_drawdown, policy.MaximumDrawdownAmount);
            _families.DataSource = policy.TradeFamilyLimits.Select(x => x with { }).ToArray();
        }
        finally { _binding = false; }
        SetActions();
    }

    void MarkDirty() { if (!_binding && _editing && _canMutate) { _dirty = true; SetActions(); } }

    async Task ActivateAsync()
    {
        if (_commands is null || _selected is not { OperatingState: PortfolioFinancialPolicyState.Draft }) return;
        var revision = await _queries.GetPortfolioRevisionAsync(_portfolio.PortfolioId);
        if (!revision.Success || revision.Value is null) { _status.Text = revision.ErrorMessage; return; }
        var result = await _commands.ActivateAndAssignAsync(new(_portfolio.PortfolioId, _selected.PolicyId), _selected.AggregateRevision, _selected.PolicyVersion, revision.Value.Revision);
        _status.Text = result.Success ? "Activation accepted; refresh Portfolio after projection completes." : result.ErrorMessage; if (result.Success) await RefreshAsync();
    }
    async Task RetireAsync() { if (_commands is null || _selected is null) return; var result = await _commands.RetirePolicyAsync(new(_portfolio.PortfolioId, _selected.PolicyId), _selected.AggregateRevision, _selected.PolicyVersion, "Operator retirement"); _status.Text = result.Success ? "Policy retired." : result.ErrorMessage; if (result.Success) await RefreshAsync(); }
    async Task DeleteAsync() { if (_commands is null || _selected is not { OperatingState: PortfolioFinancialPolicyState.Draft }) return; var result = await _commands.DeleteDraftPolicyAsync(new(_portfolio.PortfolioId, _selected.PolicyId), _selected.AggregateRevision, "Operator deleted unused Draft"); _status.Text = result.Success ? "Draft policy deleted; its ID remains consumed." : result.ErrorMessage; if (result.Success) await RefreshAsync(); }
    void SetActions()
    {
        var write = _canMutate && _commands is not null;
        _newPolicy.Enabled = write && !_editing; _newVersion.Enabled = write && !_editing && _selected is not null;
        _save.Enabled = write && _editing && _dirty; _cancel.Enabled = write && _editing;
        _activate.Enabled = write && !_editing && _selected?.OperatingState == PortfolioFinancialPolicyState.Draft;
        _retire.Enabled = write && !_editing && _selected is not null; _delete.Enabled = write && !_editing && _selected?.OperatingState == PortfolioFinancialPolicyState.Draft;
        _name.ReadOnly = !(write && _editing); _families.ReadOnly = !(write && _editing);
        foreach (var number in Numbers()) number.Enabled = write && _editing;
    }
    NumericUpDown[] Numbers() => [_capital, _reserve, _deployable, _perTrade, _aggregate, _margin, _notional, _positions, _drawdown];
    static NumericUpDown Number(decimal max = 1_000_000_000_000m) => new() { Dock = DockStyle.Fill, DecimalPlaces = 2, Maximum = max, ThousandsSeparator = true, BackColor = PortfolioUiStyle.Surface, ForeColor = PortfolioUiStyle.Foreground };
    static decimal Fit(NumericUpDown control, decimal value) => Math.Clamp(value, control.Minimum, control.Maximum);
    static Panel Pair(Control left, Control right) { var panel = new Panel { Dock = DockStyle.Fill }; left.Dock = DockStyle.Left; left.Width = 120; right.Dock = DockStyle.Fill; panel.Controls.Add(right); panel.Controls.Add(left); return panel; }
    static void Add(TableLayoutPanel panel, int row, int column, string label, Control control) { panel.Controls.Add(PortfolioUiStyle.Caption(label), column, row); panel.Controls.Add(control, column + 1, row); }
}
