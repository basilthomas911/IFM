using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed class FundMandateEditorForm : Form
{
    readonly TextBox _id = PortfolioUiStyle.TextBox("Fund ID", true);
    readonly TextBox _code = PortfolioUiStyle.TextBox("Fund code");
    readonly TextBox _name = PortfolioUiStyle.TextBox("Fund name");
    readonly NumericUpDown _year = new() { Minimum = 2000, Maximum = 2200, Dock = DockStyle.Fill };
    readonly ComboBox _state = PortfolioUiStyle.Combo("Fund operating state");
    readonly TextBox _horizon = PortfolioUiStyle.TextBox("Decision horizon");
    readonly TextBox _objective = PortfolioUiStyle.TextBox("Fund objective");
    readonly TextBox _underlyings = PortfolioUiStyle.TextBox("Underlying universe");
    readonly TextBox _assets = PortfolioUiStyle.TextBox("Eligible asset types");
    readonly TextBox _directions = PortfolioUiStyle.TextBox("Permitted directions");
    readonly TextBox _conditions = PortfolioUiStyle.TextBox("Permitted market conditions");
    readonly TextBox _families = PortfolioUiStyle.TextBox("Permitted trade families");
    readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Color.MistyRose, AutoEllipsis = true };
    readonly FundMandateReadModel? _source;
    readonly int _portfolioId;

    public FundMandateEditorForm(int portfolioId, int fundId, FundMandateReadModel? source = null)
    {
        if (portfolioId <= 0 || fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        _portfolioId = portfolioId; _source = source;
        Text = source is null ? "Create Fund Mandate" : "Create Fund Mandate Version";
        Name = "FundMandateEditorForm"; AccessibleName = Text;
        Width = 820; Height = 760; MinimizeBox = false; MaximizeBox = false;
        PortfolioUiStyle.Apply(this);
        _state.Items.AddRange(Enum.GetValues<FundOperatingState>().Where(x => x != FundOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = source?.OperatingState ?? FundOperatingState.Draft;
        _id.Text = fundId.ToString(); _code.Text = source?.FundCode ?? string.Empty; _name.Text = source?.Name ?? string.Empty;
        _year.Value = source?.TradingYear ?? DateTime.UtcNow.Year; _horizon.Text = source?.DecisionHorizon ?? "Daily";
        _objective.Text = source?.Objective ?? string.Empty; _underlyings.Text = Join(source?.UnderlyingUniverse);
        _assets.Text = Join(source?.EligibleAssetTypes); _directions.Text = Join(source?.PermittedDirections);
        _conditions.Text = Join(source?.PermittedConditions); _families.Text = Join(source?.PermittedTradeFamilies);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface };
        body.ColumnStyles.Add(new(SizeType.Absolute, 245)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(body, 0, "Fund ID", _id); Add(body, 1, "Code", _code); Add(body, 2, "Name", _name); Add(body, 3, "Trading Year", _year);
        Add(body, 4, "Operating State", _state); Add(body, 5, "Decision Horizon", _horizon); Add(body, 6, "Objective", _objective);
        Add(body, 7, "Underlyings (CSV)", _underlyings); Add(body, 8, "Asset Types (CSV)", _assets);
        Add(body, 9, "Directions (CSV)", _directions); Add(body, 10, "Market Conditions (CSV)", _conditions);
        Add(body, 11, "Trade Families (CSV)", _families); body.Controls.Add(_error, 0, 12); body.SetColumnSpan(_error, 2);
        var save = PortfolioUiStyle.Button("Save", "Save Fund mandate"); var cancel = PortfolioUiStyle.Button("Cancel", "Cancel Fund edit");
        save.Click += (_, _) => Save(); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); Controls.Add(body); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
    }

    public FundMandateReadModel? Value { get; private set; }

    void Save()
    {
        var now = DateTime.UtcNow;
        var value = new FundMandateReadModel
        {
            PortfolioId = _portfolioId, FundId = int.Parse(_id.Text), FundCode = _code.Text.Trim(), Name = _name.Text.Trim(),
            FundMandateVersion = _source is null ? 1 : checked(_source.FundMandateVersion + 1), TradingYear = (int)_year.Value,
            OperatingState = (FundOperatingState)(_state.SelectedItem ?? FundOperatingState.Draft), EffectiveFromUtc = now,
            DecisionHorizon = _horizon.Text.Trim(), Objective = _objective.Text.Trim(), UnderlyingUniverse = Csv(_underlyings.Text),
            EligibleAssetTypes = Csv(_assets.Text), PermittedDirections = Csv(_directions.Text), PermittedConditions = Csv(_conditions.Text),
            PermittedTradeFamilies = Csv(_families.Text), CreatedOnUtc = now, CreatedBy = Environment.UserName,
        };
        var errors = value.Validate(); if (errors.Count != 0) { _error.Text = string.Join("; ", errors); return; }
        Value = value; DialogResult = DialogResult.OK; Close();
    }

    static string Join(string[]? values) => string.Join(", ", values ?? []);
    static string[] Csv(string value) => [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    static void Add(TableLayoutPanel layout, int row, string caption, Control control) { layout.RowStyles.Add(new(SizeType.Absolute, 46)); layout.Controls.Add(PortfolioUiStyle.Caption(caption), 0, row); layout.Controls.Add(control, 1, row); }
}
