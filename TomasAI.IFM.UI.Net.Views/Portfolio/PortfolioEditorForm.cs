using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public sealed class PortfolioEditorForm : Form
{
    readonly TextBox _id = PortfolioUiStyle.TextBox("Portfolio ID", true);
    readonly TextBox _code = PortfolioUiStyle.TextBox("Portfolio code");
    readonly TextBox _name = PortfolioUiStyle.TextBox("Portfolio name");
    readonly TextBox _currency = PortfolioUiStyle.TextBox("Base currency");
    readonly ComboBox _state = PortfolioUiStyle.Combo("Portfolio operating state");
    readonly TextBox _policyId = PortfolioUiStyle.TextBox("Policy ID");
    readonly NumericUpDown _policyVersion = Number(0, int.MaxValue);
    readonly TextBox _brokerAccounts = PortfolioUiStyle.TextBox("Broker account references");
    readonly DateTimePicker _effective = DatePicker("Portfolio effective date");
    readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Color.MistyRose, AutoEllipsis = true };
    readonly PortfolioReadModel? _source;
    readonly bool _newPortfolio;

    public PortfolioEditorForm(int portfolioId, PortfolioReadModel? source = null)
    {
        if (portfolioId <= 0) throw new ArgumentOutOfRangeException(nameof(portfolioId));
        _source = source;
        _newPortfolio = source is null;
        Text = source is null ? "Create Portfolio" : "Create Portfolio Version";
        Name = "PortfolioEditorForm";
        AccessibleName = Text;
        Width = 720; Height = 570; MinimizeBox = false; MaximizeBox = false;
        PortfolioUiStyle.Apply(this);
        _state.Items.AddRange(Enum.GetValues<PortfolioOperatingState>().Where(x => x != PortfolioOperatingState.Unknown).Cast<object>().ToArray());
        _state.SelectedItem = source?.OperatingState ?? PortfolioOperatingState.Draft;
        _id.Text = portfolioId.ToString();
        _code.Text = source?.PortfolioCode ?? string.Empty;
        _name.Text = source?.Name ?? string.Empty;
        _currency.Text = source?.BaseCurrency ?? "USD";
        _policyId.Text = source?.PolicyId == Guid.Empty ? string.Empty : source?.PolicyId.ToString();
        _policyVersion.Value = Math.Clamp(source?.PolicyVersion ?? 0, 0, int.MaxValue);
        _brokerAccounts.Text = string.Join(", ", source?.BrokerAccountRefs ?? []);
        _effective.Value = EasternTime.FromUtc(source?.EffectiveFromUtc ?? TimeProvider.System.GetUtcNow().UtcDateTime);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface };
        body.ColumnStyles.Add(new(SizeType.Absolute, 220)); body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(body, 0, "Portfolio ID", _id); Add(body, 1, "Code", _code); Add(body, 2, "Name", _name);
        Add(body, 3, "Base Currency", _currency); Add(body, 4, "Operating State", _state);
        Add(body, 5, "Policy ID", _policyId); Add(body, 6, "Policy Version", _policyVersion);
        Add(body, 7, "Broker Accounts (CSV)", _brokerAccounts); Add(body, 8, "Effective From", _effective);
        body.Controls.Add(_error, 0, 9); body.SetColumnSpan(_error, 2);
        var save = PortfolioUiStyle.Button("Save", "Save Portfolio");
        var cancel = PortfolioUiStyle.Button("Cancel", "Cancel Portfolio edit");
        save.Click += (_, _) => Save(); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save);
        Controls.Add(body); Controls.Add(buttons);
        AcceptButton = save; CancelButton = cancel;
    }

    public PortfolioReadModel? Value { get; private set; }

    void Save()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var policyId = Guid.TryParse(_policyId.Text, out var parsedPolicy) ? parsedPolicy : Guid.Empty;
        var model = new PortfolioReadModel
        {
            PortfolioId = int.Parse(_id.Text), PortfolioCode = _code.Text.Trim(), Name = _name.Text.Trim(),
            PortfolioVersion = _newPortfolio ? 1 : checked(_source!.PortfolioVersion + 1), BaseCurrency = _currency.Text.Trim().ToUpperInvariant(),
            OperatingState = (PortfolioOperatingState)(_state.SelectedItem ?? PortfolioOperatingState.Draft),
            EffectiveFromUtc = EasternTime.ToUtc(_effective.Value), PolicyId = policyId, PolicyVersion = (long)_policyVersion.Value,
            BrokerAccountRefs = Csv(_brokerAccounts.Text), CreatedOnUtc = now, CreatedBy = Environment.UserName,
        };
        var errors = model.Validate(requireActivePolicy: model.OperatingState == PortfolioOperatingState.Active);
        if (errors.Count != 0) { _error.Text = string.Join("; ", errors); return; }
        Value = model; DialogResult = DialogResult.OK; Close();
    }

    static string[] Csv(string value) => [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    static NumericUpDown Number(decimal min, decimal max) => new() { Minimum = min, Maximum = max, Dock = DockStyle.Fill, BackColor = PortfolioUiStyle.Surface, ForeColor = PortfolioUiStyle.Foreground };
    static DateTimePicker DatePicker(string name) => new() { AccessibleName = name, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Dock = DockStyle.Fill };
    static void Add(TableLayoutPanel layout, int row, string caption, Control control) { layout.RowStyles.Add(new(SizeType.Absolute, 42)); layout.Controls.Add(PortfolioUiStyle.Caption(caption), 0, row); layout.Controls.Add(control, 1, row); }
}
