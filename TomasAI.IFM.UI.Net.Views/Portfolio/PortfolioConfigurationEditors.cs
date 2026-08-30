using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using System.ComponentModel;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

public abstract class PortfolioConfigurationEditor<T> : Form where T : class
{
    protected readonly TableLayoutPanel Body = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), BackColor = PortfolioUiStyle.Surface, AutoScroll = true };
    protected readonly Label Error = new() { Dock = DockStyle.Fill, ForeColor = Color.MistyRose, AutoEllipsis = true };

    protected PortfolioConfigurationEditor(string title, int height)
    {
        Text = title; Name = GetType().Name; AccessibleName = title; Width = 760; Height = height;
        PortfolioUiStyle.Apply(this); Body.ColumnStyles.Add(new(SizeType.Absolute, 260)); Body.ColumnStyles.Add(new(SizeType.Percent, 100));
        var save = PortfolioUiStyle.Button("Save", $"Save {title}"); var cancel = PortfolioUiStyle.Button("Cancel", $"Cancel {title}");
        save.Click += (_, _) => SaveCore(); cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6), BackColor = PortfolioUiStyle.Surface };
        buttons.Controls.Add(cancel); buttons.Controls.Add(save); Controls.Add(Body); Controls.Add(buttons); AcceptButton = save; CancelButton = cancel;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public T? Value { get; protected set; }
    protected abstract void SaveCore();
    protected void Finish(T value) { Value = value; DialogResult = DialogResult.OK; Close(); }
    protected void Add(string caption, Control control)
    {
        var row = Body.RowCount++; Body.RowStyles.Add(new(SizeType.Absolute, 44));
        Body.Controls.Add(PortfolioUiStyle.Caption(caption), 0, row); Body.Controls.Add(control, 1, row);
    }
    protected void AddError() { var row = Body.RowCount++; Body.RowStyles.Add(new(SizeType.Absolute, 52)); Body.Controls.Add(Error, 0, row); Body.SetColumnSpan(Error, 2); }
    protected static NumericUpDown Number(decimal value, decimal min = 0, decimal max = 1000000000, int decimals = 2) => new() { Value = Math.Clamp(value, min, max), Minimum = min, Maximum = max, DecimalPlaces = decimals, Dock = DockStyle.Fill, ThousandsSeparator = true };
    protected static string[] Csv(string value) => [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}

public sealed class FundAllocationEditorForm : PortfolioConfigurationEditor<FundAllocationReadModel>
{
    readonly PortfolioReadModel _portfolio; readonly FundMandateReadModel _fund; readonly FundAllocationReadModel? _source;
    readonly NumericUpDown _target; readonly NumericUpDown _minimum; readonly NumericUpDown _maximum; readonly NumericUpDown _capital; readonly TextBox _currency; readonly NumericUpDown _policyVersion;
    public FundAllocationEditorForm(PortfolioReadModel portfolio, FundMandateReadModel fund, FundAllocationReadModel? source) : base("Fund Allocation", 520)
    {
        _portfolio = portfolio; _fund = fund; _source = source;
        _target = Number(source?.TargetWeight ?? .5m, 0, 1, 4); _minimum = Number(source?.MinimumWeight ?? 0, 0, 1, 4); _maximum = Number(source?.MaximumWeight ?? 1, 0, 1, 4);
        _capital = Number(source?.AllocatedCapital ?? 0); _currency = PortfolioUiStyle.TextBox("Allocation currency"); _currency.Text = source?.Currency ?? portfolio.BaseCurrency;
        _policyVersion = Number(source?.SourcePolicyVersion ?? Math.Max(1, portfolio.PolicyVersion), 1, long.MaxValue, 0);
        Add("Target Weight", _target); Add("Minimum Weight", _minimum); Add("Maximum Weight", _maximum); Add("Allocated Capital", _capital); Add("Currency", _currency); Add("Source Policy Version", _policyVersion); AddError();
    }
    protected override void SaveCore()
    {
        var now = DateTime.UtcNow; var value = new FundAllocationReadModel { PortfolioId = _portfolio.PortfolioId, PortfolioVersion = _portfolio.PortfolioVersion, FundId = _fund.FundId, FundMandateVersion = _fund.FundMandateVersion, AllocationVersion = (_source?.AllocationVersion ?? 0) + 1, TargetWeight = _target.Value, MinimumWeight = _minimum.Value, MaximumWeight = _maximum.Value, AllocatedCapital = _capital.Value, Currency = _currency.Text.Trim().ToUpperInvariant(), EffectiveFromUtc = now, SourcePolicyVersion = (long)_policyVersion.Value, CreatedOnUtc = now, CreatedBy = Environment.UserName };
        var errors = value.Validate(); if (errors.Count != 0) { Error.Text = string.Join("; ", errors); return; } Finish(value);
    }
}

public sealed class FundRiskEnvelopeEditorForm : PortfolioConfigurationEditor<FundRiskEnvelopeReadModel>
{
    readonly PortfolioReadModel _portfolio; readonly FundMandateReadModel _fund; readonly FundRiskEnvelopeReadModel? _source;
    readonly ComboBox _capacity = PortfolioUiStyle.Combo("Capacity state"); readonly TextBox _currency = PortfolioUiStyle.TextBox("Risk currency");
    readonly NumericUpDown _allocated; readonly NumericUpDown _available; readonly NumericUpDown _perTrade; readonly NumericUpDown _aggregate; readonly NumericUpDown _margin; readonly NumericUpDown _notional; readonly NumericUpDown _contracts; readonly NumericUpDown _positions; readonly NumericUpDown _lossBudget; readonly NumericUpDown _days;
    public FundRiskEnvelopeEditorForm(PortfolioReadModel portfolio, FundMandateReadModel fund, FundRiskEnvelopeReadModel? source) : base("Fund Risk Envelope", 720)
    {
        _portfolio = portfolio; _fund = fund; _source = source; _capacity.Items.AddRange(Enum.GetValues<FundCapacityState>().Where(x => x != FundCapacityState.Unknown).Cast<object>().ToArray()); _capacity.SelectedItem = source?.CapacityState ?? FundCapacityState.Available;
        _currency.Text = source?.Currency ?? portfolio.BaseCurrency; _allocated = Number(source?.AllocatedCapital ?? 0); _available = Number(source?.AvailableCapital ?? 0); _perTrade = Number(source?.MaximumRiskPerTrade ?? 0); _aggregate = Number(source?.MaximumAggregateRisk ?? 0); _margin = Number(source?.MaximumMargin ?? 0); _notional = Number(source?.MaximumGrossNotional ?? 0); _contracts = Number(source?.MaximumContracts ?? 0, 0, int.MaxValue, 0); _positions = Number(source?.MaximumOpenPositions ?? 0, 0, int.MaxValue, 0); _lossBudget = Number(source?.RemainingLossBudget ?? 0); _days = Number(30, 1, 3650, 0);
        Add("Capacity State", _capacity); Add("Currency", _currency); Add("Allocated Capital", _allocated); Add("Available Capital", _available); Add("Maximum Risk / Trade", _perTrade); Add("Maximum Aggregate Risk", _aggregate); Add("Maximum Margin", _margin); Add("Maximum Gross Notional", _notional); Add("Maximum Contracts", _contracts); Add("Maximum Open Positions", _positions); Add("Remaining Loss Budget", _lossBudget); Add("Effective Days", _days); AddError();
    }
    protected override void SaveCore()
    {
        var now = DateTime.UtcNow; var value = new FundRiskEnvelopeReadModel { PortfolioId = _portfolio.PortfolioId, PortfolioVersion = _portfolio.PortfolioVersion, FundId = _fund.FundId, FundMandateVersion = _fund.FundMandateVersion, EnvelopeId = _source?.EnvelopeId ?? Guid.NewGuid(), EnvelopeVersion = (_source?.EnvelopeVersion ?? 0) + 1, CapacityState = (FundCapacityState)(_capacity.SelectedItem ?? FundCapacityState.Available), Currency = _currency.Text.Trim().ToUpperInvariant(), AllocatedCapital = _allocated.Value, AvailableCapital = _available.Value, MaximumRiskPerTrade = _perTrade.Value, MaximumAggregateRisk = _aggregate.Value, MaximumMargin = _margin.Value, MaximumGrossNotional = _notional.Value, MaximumContracts = (int)_contracts.Value, MaximumOpenPositions = (int)_positions.Value, RemainingLossBudget = _lossBudget.Value, EffectiveFromUtc = now, ExpiresAtUtc = now.AddDays((double)_days.Value), SourcePolicyId = _portfolio.PolicyId == Guid.Empty ? Guid.NewGuid() : _portfolio.PolicyId, SourcePolicyVersion = Math.Max(1, _portfolio.PolicyVersion), CreatedOnUtc = now, CreatedBy = Environment.UserName };
        var errors = value.Validate(); if (errors.Count != 0) { Error.Text = string.Join("; ", errors); return; } Finish(value);
    }
}

public sealed class FundAssignmentEditorForm : PortfolioConfigurationEditor<FundTradeTemplateAssignmentReadModel>
{
    readonly PortfolioReadModel _portfolio; readonly FundMandateReadModel _fund;
    readonly TextBox _template = PortfolioUiStyle.TextBox("Trade template ID"); readonly NumericUpDown _templateVersion = Number(1, 1, long.MaxValue, 0);
    readonly TextBox _horizon = PortfolioUiStyle.TextBox("Assignment decision horizon"); readonly TextBox _underlyings = PortfolioUiStyle.TextBox("Assignment underlyings"); readonly TextBox _asset = PortfolioUiStyle.TextBox("Assignment asset type"); readonly TextBox _family = PortfolioUiStyle.TextBox("Assignment trade family"); readonly NumericUpDown _priority = Number(0, 0, int.MaxValue, 0);
    readonly TextBox _hint = PortfolioUiStyle.TextBox("Trade selection hint profile ID"); readonly NumericUpDown _hintVersion = Number(1, 1, long.MaxValue, 0); readonly TextBox _composition = PortfolioUiStyle.TextBox("Order composition profile ID"); readonly NumericUpDown _compositionVersion = Number(1, 1, long.MaxValue, 0);
    public FundAssignmentEditorForm(PortfolioReadModel portfolio, FundMandateReadModel fund) : base("Trade Template Assignment", 720)
    {
        _portfolio = portfolio; _fund = fund; _horizon.Text = fund.DecisionHorizon; _underlyings.Text = string.Join(", ", fund.UnderlyingUniverse); _asset.Text = fund.EligibleAssetTypes.FirstOrDefault() ?? string.Empty; _family.Text = fund.PermittedTradeFamilies.FirstOrDefault() ?? string.Empty;
        Add("Trade Template ID", _template); Add("Template Version", _templateVersion); Add("Decision Horizon", _horizon); Add("Underlyings (CSV)", _underlyings); Add("Asset Type", _asset); Add("Trade Family", _family); Add("Priority", _priority); Add("Selection Hint Profile ID", _hint); Add("Hint Profile Version", _hintVersion); Add("Composition Profile ID", _composition); Add("Composition Version", _compositionVersion); AddError();
    }
    protected override void SaveCore()
    {
        var now = DateTime.UtcNow; var value = new FundTradeTemplateAssignmentReadModel { PortfolioId = _portfolio.PortfolioId, PortfolioVersion = _portfolio.PortfolioVersion, FundId = _fund.FundId, FundMandateVersion = _fund.FundMandateVersion, AssignmentVersion = 1, TradeTemplateId = Guid.TryParse(_template.Text, out var template) ? template : Guid.Empty, TradeTemplateVersion = (long)_templateVersion.Value, Enabled = true, DecisionHorizon = _horizon.Text.Trim(), UnderlyingUniverse = Csv(_underlyings.Text), AssetType = _asset.Text.Trim(), TradeFamily = _family.Text.Trim(), Priority = (int)_priority.Value, EffectiveFromUtc = now, TradeSelectionHintProfileId = Guid.TryParse(_hint.Text, out var hint) ? hint : Guid.Empty, TradeSelectionHintProfileVersion = (long)_hintVersion.Value, OrderCompositionProfileId = Guid.TryParse(_composition.Text, out var composition) ? composition : Guid.Empty, OrderCompositionProfileVersion = (long)_compositionVersion.Value, CreatedOnUtc = now, CreatedBy = Environment.UserName };
        var errors = value.Validate(); if (errors.Count != 0) { Error.Text = string.Join("; ", errors); return; } Finish(value);
    }
}
