using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed class FundMandateEditorForm : DarkTradingForm
{
    readonly TextBox _id = PortfolioUiStyle.TextBox("Fund ID", true);
    readonly TextBox _name = PortfolioUiStyle.TextBox("Fund name");
    readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2200, Dock = DockStyle.Fill };
    readonly ComboBox _state = PortfolioUiStyle.Combo("Fund operating state");
    readonly ComboBox _horizon = PortfolioUiStyle.StrategyTimeFrameCombo("Decision horizon");
    readonly TextBox _objective = PortfolioUiStyle.TextBox("Fund objective");
    readonly CheckedDropdown _underlyings = new() { AccessibleName = "Underlying universe" };
    readonly CheckedDropdown _assets = new() { AccessibleName = "Eligible asset types" };
    readonly CheckedDropdown _directions = new() { AccessibleName = "Permitted directions" };
    readonly CheckedDropdown _conditions = new() { AccessibleName = "Permitted market conditions" };
    readonly CheckedListBox _families = new() { AccessibleName = "Permitted trade families", Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true, BackColor = PortfolioUiStyle.DataSurface, ForeColor = PortfolioUiStyle.Foreground, BorderStyle = BorderStyle.FixedSingle };
    readonly HashSet<TradeStrategyFamilyReference> _activeFamilyReferences;
    readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Color.MistyRose, AutoEllipsis = true };
    readonly FundMandateReadModel? _source;
    readonly int _portfolioId;
    readonly int _fundId;
    readonly FundSelectionCatalog? _selections;

    public FundMandateEditorForm(int portfolioId, int fundId, FundMandateReadModel? source = null, IEnumerable<StrategyDeploymentChoice>? catalog = null,
        FundSelectionCatalog? selections = null)
    {
        if (portfolioId <= 0 || fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        _portfolioId = portfolioId; _fundId = fundId; _source = source; _selections = selections;
        var active = TradeFamilyCatalogSelection.Active(catalog);
        _activeFamilyReferences = active.Select(TradeStrategyFamilyReference.From).ToHashSet();
        Text = source is null ? "Create Fund" : "Change Fund";
        Name = "FundMandateEditorForm"; AccessibleName = Text;
        Width = 960; Height = 850; MinimizeBox = false; MaximizeBox = false;
        PortfolioUiStyle.Apply(this);
        _state.Items.AddRange(Enum.GetValues<FundOperatingState>().Where(x => x != FundOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = source?.OperatingState ?? FundOperatingState.Draft;
        if (source is null) _state.Enabled = false; // Creation always begins in Draft.
        _id.Text = fundId.ToString(); _name.Text = source?.Name ?? string.Empty;
        _year.Value = source?.TradingYear ?? DateTime.UtcNow.Year;
        PortfolioUiStyle.SelectStrategyTimeFrame(_horizon, source is null ? "Daily" : source.DecisionHorizon);
        _objective.Text = source?.Objective ?? string.Empty;
        _underlyings.SetItems((selections?.Underlyings ?? []).Select(x => new CheckedDropdownItem(x, x)));
        SetLookupItems(_assets, selections?.AssetTypes); SetLookupItems(_directions, selections?.Directions); SetLookupItems(_conditions, selections?.MarketConditions);
        _underlyings.SetSelectedValues(source?.UnderlyingUniverse ?? []); _assets.SetSelectedValues(source?.EligibleAssetTypes ?? []);
        _directions.SetSelectedValues(source?.PermittedDirections ?? []); _conditions.SetSelectedValues(source?.PermittedConditions ?? []);
        var selectedReferences = (source?.PermittedTradeStrategyFamilies ?? []).ToHashSet();
        var selectedKeys = selectedReferences.Count == 0 ? (source?.PermittedTradeFamilies ?? []).ToHashSet(StringComparer.Ordinal) : [];
        var resolvedLegacy = new HashSet<string>(StringComparer.Ordinal); // Legacy names never grant new deployment permissions.
        foreach (var row in active)
            _families.Items.Add(TradeFamilyCatalogSelection.Choice.From(row), selectedReferences.Contains(TradeStrategyFamilyReference.From(row)) || resolvedLegacy.Contains(row.SystemKey));
        var unresolved = selectedKeys.Except(resolvedLegacy, StringComparer.Ordinal).ToArray();
        foreach (var key in unresolved)
            _families.Items.Add(new TradeFamilyCatalogSelection.Choice(key, $"Unavailable: {key} — uncheck to remove"), true);
        foreach (var reference in selectedReferences.Except(_activeFamilyReferences))
            _families.Items.Add(new TradeFamilyCatalogSelection.Choice("", $"Unavailable: {reference.CatalogDeployment?.Id.ToString() ?? reference.TradeStrategyFamilyId.ToString()} v{reference.CatalogDeployment?.Version.ToString() ?? reference.DefinitionVersion.ToString()} — uncheck to remove", reference), true);
        if (active.Length == 0) _error.Text = "No strategy deployments are configured. You can save a Draft Fund without permissions. Uncheck any unavailable entries to remove them.";
        else if (unresolved.Length != 0 || selectedReferences.Except(_activeFamilyReferences).Any()) _error.Text = "Some existing families are unavailable or ambiguous. Explicitly remove them and select exact catalog replacements before saving.";

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 12, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface };
        body.ColumnStyles.Add(new(SizeType.Absolute, 245)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(body, 0, "Fund ID", _id); Add(body, 1, "Name", _name); Add(body, 2, "Trading Year", _year);
        Add(body, 3, "Operating State", _state); Add(body, 4, "Decision Horizon", _horizon); Add(body, 5, "Objective", _objective);
        Add(body, 6, "Underlyings", _underlyings); Add(body, 7, "Asset Types", _assets);
        Add(body, 8, "Directions", _directions); Add(body, 9, "Market Conditions", _conditions);
        Add(body, 10, "Permitted Trade Families", _families);
        body.Controls.Add(_error, 0, 11); body.SetColumnSpan(_error, 2);
        var save = PortfolioUiStyle.Button("Save", "Save Fund mandate"); var cancel = PortfolioUiStyle.Button("Cancel", "Cancel Fund edit");
        save.Click += (_, _) => Save(); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); Controls.Add(body); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
    }

    public FundMandateReadModel? Value { get; private set; }

    void Save()
    {
        if (_selections is null) { _error.Text = "Fund selection lists are unavailable. Reload the editor."; return; }
        try { _selections.ValidateSelections(_underlyings.SelectedValues, _assets.SelectedValues, _directions.SelectedValues, _conditions.SelectedValues); }
        catch (ArgumentException ex) { _error.Text = ex.Message; return; }
        var choices = _families.CheckedItems.Cast<TradeFamilyCatalogSelection.Choice>().ToArray();
        var state = (FundOperatingState)(_state.SelectedItem ?? FundOperatingState.Draft);
        var permitsEmpty = state is FundOperatingState.Draft or FundOperatingState.Disabled or FundOperatingState.Retired;
        if ((choices.Length == 0 && !permitsEmpty) || choices.Any(x => x.Reference is null || !_activeFamilyReferences.Contains(x.Reference)))
        {
            _error.Text = "Select valid strategy deployments, or save as Draft without permissions. Explicitly uncheck unavailable entries before saving.";
            return;
        }
        var now = DateTime.UtcNow;
        var value = new FundMandateReadModel
        {
            PortfolioId = _portfolioId, FundId = _fundId,
            // Compatibility metadata only: never ask the operator to invent a second Fund identifier.
            FundCode = _source?.FundCode ?? "FUND-" + _fundId.ToString(System.Globalization.CultureInfo.InvariantCulture), Name = _name.Text.Trim(),
            FundMandateVersion = _source is null ? 1 : checked(_source.FundMandateVersion + 1), TradingYear = (int)_year.Value,
            OperatingState = state, EffectiveFromUtc = now,
            DecisionHorizon = PortfolioUiStyle.SelectedStrategyTimeFrameName(_horizon), Objective = _objective.Text.Trim(), UnderlyingUniverse = _underlyings.SelectedValues,
            EligibleAssetTypes = _assets.SelectedValues, PermittedDirections = _directions.SelectedValues, PermittedConditions = _conditions.SelectedValues,
            SchemaVersion = 3, PermittedTradeStrategyFamilies = choices.Select(x => x.Reference!).ToArray(),
            PermittedTradeFamilies = choices.Select(x => x.SystemKey).Distinct(StringComparer.Ordinal).ToArray(), CreatedOnUtc = now, CreatedBy = Environment.UserName,
            HistoricalSource = _source?.HistoricalSource ?? string.Empty, HistoricalSourceFundId = _source?.HistoricalSourceFundId,
        };
        var errors = value.Validate(); if (errors.Count != 0) { _error.Text = string.Join("; ", errors); return; }
        Value = value; DialogResult = DialogResult.OK; Close();
    }

    static void SetLookupItems(CheckedDropdown control, LookupDefinitionReadModel[]? rows)
        => control.SetItems((rows ?? []).Select(x => new CheckedDropdownItem(x.InternalValue, x.DisplayName, FundSelectionCatalog.IsSelectable(x))));
    static void Add(TableLayoutPanel layout, int row, string caption, Control control)
    {
        var isList = control is ListBox;
        layout.RowStyles.Add(new(SizeType.Absolute, isList ? 120 : 46));
        var label = PortfolioUiStyle.Caption(caption);
        label.AutoSize = false;
        label.Padding = isList ? new Padding(0, 2, 0, 0) : Padding.Empty;
        label.TextAlign = isList ? ContentAlignment.TopRight : ContentAlignment.MiddleRight;
        if (!isList)
        {
            control.Dock = DockStyle.None;
            control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        }
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(control, 1, row);
    }
}
