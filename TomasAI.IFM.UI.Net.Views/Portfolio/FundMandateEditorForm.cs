using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed class FundMandateEditorForm : Form
{
    readonly TextBox _id = PortfolioUiStyle.TextBox("Fund ID", true);
    readonly TextBox _code = PortfolioUiStyle.TextBox("Fund code");
    readonly TextBox _name = PortfolioUiStyle.TextBox("Fund name");
    readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2200, Dock = DockStyle.Fill };
    readonly ComboBox _state = PortfolioUiStyle.Combo("Fund operating state");
    readonly ComboBox _horizon = PortfolioUiStyle.StrategyTimeFrameCombo("Decision horizon");
    readonly TextBox _objective = PortfolioUiStyle.TextBox("Fund objective");
    readonly TextBox _underlyings = PortfolioUiStyle.TextBox("Underlying universe");
    readonly TextBox _assets = PortfolioUiStyle.TextBox("Eligible asset types");
    readonly TextBox _directions = PortfolioUiStyle.TextBox("Permitted directions");
    readonly TextBox _conditions = PortfolioUiStyle.TextBox("Permitted market conditions");
    readonly CheckedListBox _families = new() { AccessibleName = "Permitted trade families", Dock = DockStyle.Fill, CheckOnClick = true, IntegralHeight = false, HorizontalScrollbar = true, BackColor = PortfolioUiStyle.DataSurface, ForeColor = PortfolioUiStyle.Foreground, BorderStyle = BorderStyle.FixedSingle };
    readonly HashSet<TradeStrategyFamilyReference> _activeFamilyReferences;
    readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Color.MistyRose, AutoEllipsis = true };
    readonly FundMandateReadModel? _source;
    readonly int _portfolioId;

    public FundMandateEditorForm(int portfolioId, int fundId, FundMandateReadModel? source = null, IEnumerable<TradeStrategyFamilyReadModel>? catalog = null)
    {
        if (portfolioId <= 0 || fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        _portfolioId = portfolioId; _source = source;
        var active = TradeFamilyCatalogSelection.Active(catalog);
        _activeFamilyReferences = active.Select(TradeStrategyFamilyReference.From).ToHashSet();
        Text = source is null ? "Create Fund Mandate" : "Create Fund Mandate Version";
        Name = "FundMandateEditorForm"; AccessibleName = Text;
        Width = 960; Height = 850; MinimizeBox = false; MaximizeBox = false;
        PortfolioUiStyle.Apply(this);
        _state.Items.AddRange(Enum.GetValues<FundOperatingState>().Where(x => x != FundOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = source?.OperatingState ?? FundOperatingState.Draft;
        _id.Text = fundId.ToString(); _code.Text = source?.FundCode ?? string.Empty; _name.Text = source?.Name ?? string.Empty;
        _year.Value = source?.TradingYear ?? DateTime.UtcNow.Year;
        PortfolioUiStyle.SelectStrategyTimeFrame(_horizon, source is null ? "Daily" : source.DecisionHorizon);
        _objective.Text = source?.Objective ?? string.Empty; _underlyings.Text = Join(source?.UnderlyingUniverse);
        _assets.Text = Join(source?.EligibleAssetTypes); _directions.Text = Join(source?.PermittedDirections);
        _conditions.Text = Join(source?.PermittedConditions);
        var selectedReferences = (source?.PermittedTradeStrategyFamilies ?? []).ToHashSet();
        var selectedKeys = selectedReferences.Count == 0 ? (source?.PermittedTradeFamilies ?? []).ToHashSet(StringComparer.Ordinal) : [];
        var resolvedLegacy = selectedKeys.Where(key => active.Count(x => x.SystemKey == key) == 1).ToHashSet(StringComparer.Ordinal);
        foreach (var row in active)
            _families.Items.Add(TradeFamilyCatalogSelection.Choice.From(row), selectedReferences.Contains(TradeStrategyFamilyReference.From(row)) || resolvedLegacy.Contains(row.SystemKey));
        var unresolved = selectedKeys.Except(resolvedLegacy, StringComparer.Ordinal).ToArray();
        foreach (var key in unresolved)
            _families.Items.Add(new TradeFamilyCatalogSelection.Choice(key, $"Unavailable: {key} — uncheck to remove"), true);
        foreach (var reference in selectedReferences.Except(_activeFamilyReferences))
            _families.Items.Add(new TradeFamilyCatalogSelection.Choice("", $"Unavailable: ID {reference.TradeStrategyFamilyId} v{reference.DefinitionVersion} — uncheck to remove", reference), true);
        if (active.Length == 0) _error.Text = "No active trade strategy families are available. Reload the catalog before saving.";
        else if (unresolved.Length != 0 || selectedReferences.Except(_activeFamilyReferences).Any()) _error.Text = "Some existing families are unavailable or ambiguous. Explicitly remove them and select exact catalog replacements before saving.";

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface };
        body.ColumnStyles.Add(new(SizeType.Absolute, 245)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(body, 0, "Fund ID", _id); Add(body, 1, "Code", _code); Add(body, 2, "Name", _name); Add(body, 3, "Trading Year", _year);
        Add(body, 4, "Operating State", _state); Add(body, 5, "Decision Horizon", _horizon); Add(body, 6, "Objective", _objective);
        Add(body, 7, "Underlyings (CSV)", _underlyings); Add(body, 8, "Asset Types (CSV)", _assets);
        Add(body, 9, "Directions (CSV)", _directions); Add(body, 10, "Market Conditions (CSV)", _conditions);
        Add(body, 11, "Permitted Trade Families", _families); body.RowStyles[11].Height = 120;
        body.Controls.Add(_error, 0, 12); body.SetColumnSpan(_error, 2);
        var save = PortfolioUiStyle.Button("Save", "Save Fund mandate"); var cancel = PortfolioUiStyle.Button("Cancel", "Cancel Fund edit");
        save.Click += (_, _) => Save(); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); Controls.Add(body); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
    }

    public FundMandateReadModel? Value { get; private set; }

    void Save()
    {
        var choices = _families.CheckedItems.Cast<TradeFamilyCatalogSelection.Choice>().ToArray();
        if (choices.Length == 0 || choices.Any(x => x.Reference is null || !_activeFamilyReferences.Contains(x.Reference)))
        {
            _error.Text = "Select at least one active catalog family; explicitly uncheck unavailable entries before saving.";
            return;
        }
        var now = DateTime.UtcNow;
        var value = new FundMandateReadModel
        {
            PortfolioId = _portfolioId, FundId = int.Parse(_id.Text), FundCode = _code.Text.Trim(), Name = _name.Text.Trim(),
            FundMandateVersion = _source is null ? 1 : checked(_source.FundMandateVersion + 1), TradingYear = (int)_year.Value,
            OperatingState = (FundOperatingState)(_state.SelectedItem ?? FundOperatingState.Draft), EffectiveFromUtc = now,
            DecisionHorizon = PortfolioUiStyle.SelectedStrategyTimeFrameName(_horizon), Objective = _objective.Text.Trim(), UnderlyingUniverse = Csv(_underlyings.Text),
            EligibleAssetTypes = Csv(_assets.Text), PermittedDirections = Csv(_directions.Text), PermittedConditions = Csv(_conditions.Text),
            SchemaVersion = 2, PermittedTradeStrategyFamilies = choices.Select(x => x.Reference!).ToArray(),
            PermittedTradeFamilies = choices.Select(x => x.SystemKey).Distinct(StringComparer.Ordinal).ToArray(), CreatedOnUtc = now, CreatedBy = Environment.UserName,
        };
        var errors = value.Validate(); if (errors.Count != 0) { _error.Text = string.Join("; ", errors); return; }
        Value = value; DialogResult = DialogResult.OK; Close();
    }

    static string Join(string[]? values) => string.Join(", ", values ?? []);
    static string[] Csv(string value) => [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    static void Add(TableLayoutPanel layout, int row, string caption, Control control) { layout.RowStyles.Add(new(SizeType.Absolute, 46)); layout.Controls.Add(PortfolioUiStyle.Caption(caption), 0, row); layout.Controls.Add(control, 1, row); }
}
